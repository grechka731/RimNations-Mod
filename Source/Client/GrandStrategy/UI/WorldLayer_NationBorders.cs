using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorld.Planet;
using RimWorldOnlineCity;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    /// <summary>
    /// Worker Gamma: 3D Spherical Territorial Border Renderer.
    /// Projects colored sovereign territorial polygons directly on the 3D world sphere.
    /// Every nation (player, online foreign nations, enemies) gets its own unique, mathematically
    /// distinct high-contrast color rendered through dedicated per-nation GPU batches.
    /// </summary>
    public class WorldComponent_NationBorders : WorldComponent
    {
        public static WorldComponent_NationBorders Instance { get; private set; }

        /// <summary>
        /// Sovereign territory of the local player (used for customs, borders, and contiguity).
        /// </summary>
        public HashSet<int> ClaimedTiles { get; } = new HashSet<int>();

        private class NationRenderBatch
        {
            public string OwnerKey;
            public Color BaseColor;
            public Mesh FillMesh;
            public Mesh OutlineMesh;
            public Material FillMat;
            public Material OutlineMat;
        }

        private List<NationRenderBatch> batches = new List<NationRenderBatch>();
        private bool dirty = true;
        private float lastRebuildRealtime = -1f;
        private float lastLiveSyncRealtime = -1f;

        public WorldComponent_NationBorders(World world) : base(world)
        {
            Instance = this;
        }

        public void SetDirty()
        {
            dirty = true;
        }

        public bool IsTileInPlayerTerritory(int tile)
        {
            return ClaimedTiles.Contains(tile);
        }

        /// <summary>
        /// Generates an exact, arbitrary organic cluster of connected hexes around a center tile:
        /// - Capital: 10 to 13 tiles total
        /// - Secondary City: 7 to 10 tiles total
        /// Deterministically seeded so every client computes identical boundaries.
        /// </summary>
        public static HashSet<int> GenerateOrganicTerritory(int centerTile, bool isCapital)
        {
            var claimed = new HashSet<int>();
            if (centerTile < 0 || Find.WorldGrid == null) return claimed;

            int seed = centerTile * 10007 + (isCapital ? 777 : 123);
            var rng = new System.Random(seed);
            int targetCount = isCapital ? rng.Next(10, 14) : rng.Next(7, 11);

            claimed.Add(centerTile);
            var candidates = new List<int>();
            var neighbors = new List<PlanetTile>();

            Find.WorldGrid.GetTileNeighbors((PlanetTile)centerTile, neighbors);
            foreach (var pt in neighbors)
            {
                if (pt.tileId >= 0 && !candidates.Contains(pt.tileId))
                {
                    candidates.Add(pt.tileId);
                }
            }

            while (claimed.Count < targetCount && candidates.Count > 0)
            {
                int chosenIdx = rng.Next(candidates.Count);
                int chosenTile = candidates[chosenIdx];
                candidates.RemoveAt(chosenIdx);

                if (claimed.Add(chosenTile))
                {
                    neighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors((PlanetTile)chosenTile, neighbors);
                    foreach (var pt in neighbors)
                    {
                        if (pt.tileId >= 0 && !claimed.Contains(pt.tileId) && !candidates.Contains(pt.tileId))
                        {
                            candidates.Add(pt.tileId);
                        }
                    }
                }
            }

            return claimed;
        }

        public override void WorldComponentUpdate()
        {
            base.WorldComponentUpdate();

            if (!WorldRendererUtility.WorldRendered) return;

            float now = Time.realtimeSinceStartup;

            // Live synchronization: poll server state every 2.5 seconds even while in UI/starting menus
            if (now - lastLiveSyncRealtime > 2.5f)
            {
                lastLiveSyncRealtime = now;
                if (SessionClientController.Data != null && SessionClient.Get != null && SessionClient.Get.IsLogined)
                {
                    SessionClientController.UpdateWorldSafelyRun = () =>
                    {
                        SetDirty();
                    };
                }
            }

            if (dirty || batches.Count == 0 || (now - lastRebuildRealtime > 3.0f))
            {
                RegenerateMesh();
            }

            DrawMeshDirect();
        }

        public void DrawMeshDirect()
        {
            if (batches == null || batches.Count == 0) return;

            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch.FillMesh != null && batch.FillMesh.vertexCount > 0 && batch.FillMat != null)
                {
                    Graphics.DrawMesh(batch.FillMesh, Vector3.zero, Quaternion.identity, batch.FillMat, WorldCameraManager.WorldLayer);
                }
                if (batch.OutlineMesh != null && batch.OutlineMesh.vertexCount > 0 && batch.OutlineMat != null)
                {
                    Graphics.DrawMesh(batch.OutlineMesh, Vector3.zero, Quaternion.identity, batch.OutlineMat, WorldCameraManager.WorldLayer);
                }
            }
        }

        public void RegenerateMesh()
        {
            dirty = false;
            lastRebuildRealtime = Time.realtimeSinceStartup;

            if (Find.WorldGrid == null || Find.WorldObjects == null) return;

            ClaimedTiles.Clear();
            batches.Clear();

            var allClaimedTiles = new HashSet<int>();
            var gov = WorldComponent_StateGovernor.Instance;
            string myLogin = SessionClientController.My?.Login ?? "Player";

            // 1. Process Local Player's Sovereign Settlements
            var settlements = Find.WorldObjects.Settlements;
            var playerSettlements = settlements?.Where(s => s != null && s.Faction != null && s.Faction.IsPlayer).ToList();

            if (playerSettlements != null && playerSettlements.Count > 0)
            {
                Color localColor = gov != null ? gov.GetEffectiveColor() : WorldComponent_StateGovernor.GetColorFromLogin(myLogin, 0.50f);
                var localNationTiles = new HashSet<int>();

                foreach (var s in playerSettlements)
                {
                    bool isCap = (gov != null && gov.CapitalTile == s.Tile) || (playerSettlements.Count == 1);
                    var sTiles = GenerateOrganicTerritory(s.Tile, isCap);
                    foreach (int t in sTiles)
                    {
                        localNationTiles.Add(t);
                    }
                }

                foreach (int t in localNationTiles)
                {
                    ClaimedTiles.Add(t);
                    allClaimedTiles.Add(t);
                }

                var batch = CreateNationBatch(myLogin, localColor, localNationTiles);
                if (batch != null) batches.Add(batch);
            }

            // 2. Process Other Online Players' & Foreign/Hostile Settlements
            var otherBases = Find.WorldObjects.AllWorldObjects?.OfType<BaseOnline>().ToList();
            if (otherBases != null && otherBases.Count > 0)
            {
                var basesByLogin = otherBases
                    .Where(b => b.OnlineWObject != null && !string.IsNullOrEmpty(b.OnlineWObject.LoginOwner))
                    .GroupBy(b => b.OnlineWObject.LoginOwner);

                foreach (var group in basesByLogin)
                {
                    string playerLogin = group.Key;
                    if (!string.IsNullOrEmpty(myLogin) && playerLogin == myLogin) continue;

                    Color otherPlayerColor = WorldComponent_StateGovernor.GetColorFromLogin(playerLogin, 0.50f);

                    var foreignNationTiles = new HashSet<int>();
                    foreach (var b in group)
                    {
                        bool isCap = b.OnlineWObject?.IsCapital ?? false;
                        var bTiles = GenerateOrganicTerritory(b.Tile, isCap);
                        foreach (int t in bTiles)
                        {
                            foreignNationTiles.Add(t);
                        }
                    }

                    // Prevent foreign tiles from overlapping existing local claimed territory
                    foreignNationTiles.RemoveWhere(t => allClaimedTiles.Contains(t));
                    foreach (int t in foreignNationTiles)
                    {
                        allClaimedTiles.Add(t);
                    }

                    if (foreignNationTiles.Count > 0)
                    {
                        var foreignBatch = CreateNationBatch(playerLogin, otherPlayerColor, foreignNationTiles);
                        if (foreignBatch != null) batches.Add(foreignBatch);
                    }
                }
            }
        }

        private NationRenderBatch CreateNationBatch(string ownerKey, Color baseColor, HashSet<int> tiles)
        {
            if (tiles == null || tiles.Count == 0) return null;

            var fillVerts = new List<Vector3>();
            var fillUvs = new List<Vector2>();
            var fillTris = new List<int>();

            var outlineVerts = new List<Vector3>();
            var outlineUvs = new List<Vector2>();
            var outlineTris = new List<int>();

            var tileVerts = new List<Vector3>();
            var neighborCheck = new List<PlanetTile>();

            foreach (int tile in tiles)
            {
                tileVerts.Clear();
                Find.WorldGrid.GetTileVertices((PlanetTile)tile, tileVerts);
                if (tileVerts.Count < 3) continue;

                Vector3 center = Find.WorldGrid.GetTileCenter(tile);
                center += center.normalized * 0.025f;

                int centerIndex = fillVerts.Count;
                fillVerts.Add(center);
                fillUvs.Add(new Vector2(0.5f, 0.5f));

                int startVertIndex = fillVerts.Count;
                for (int v = 0; v < tileVerts.Count; v++)
                {
                    Vector3 vertPos = tileVerts[v] + tileVerts[v].normalized * 0.025f;
                    fillVerts.Add(vertPos);
                    fillUvs.Add(new Vector2(0.5f, 0.5f));
                }

                // Double-sided fill triangles
                for (int v = 0; v < tileVerts.Count; v++)
                {
                    int next = (v + 1) % tileVerts.Count;
                    // Clockwise
                    fillTris.Add(centerIndex);
                    fillTris.Add(startVertIndex + v);
                    fillTris.Add(startVertIndex + next);

                    // Counter-clockwise
                    fillTris.Add(centerIndex);
                    fillTris.Add(startVertIndex + next);
                    fillTris.Add(startVertIndex + v);
                }

                // Outer border outline ribbon
                neighborCheck.Clear();
                Find.WorldGrid.GetTileNeighbors((PlanetTile)tile, neighborCheck);
                for (int v = 0; v < tileVerts.Count; v++)
                {
                    int next = (v + 1) % tileVerts.Count;
                    Vector3 edgeMid = (tileVerts[v] + tileVerts[next]) * 0.5f;

                    // Check if neighbor opposite to this edge is outside the nation
                    bool isExteriorEdge = false;
                    foreach (var n in neighborCheck)
                    {
                        if (!tiles.Contains(n.tileId))
                        {
                            Vector3 nCenter = Find.WorldGrid.GetTileCenter(n.tileId);
                            if (Vector3.Dot((nCenter - center).normalized, (edgeMid - center).normalized) > 0.35f)
                            {
                                isExteriorEdge = true;
                                break;
                            }
                        }
                    }

                    if (isExteriorEdge)
                    {
                        Vector3 v1 = tileVerts[v] + tileVerts[v].normalized * 0.028f;
                        Vector3 v2 = tileVerts[next] + tileVerts[next].normalized * 0.028f;
                        Vector3 v1_in = Vector3.Lerp(v1, center, 0.22f);
                        Vector3 v2_in = Vector3.Lerp(v2, center, 0.22f);

                        int bIdx = outlineVerts.Count;
                        outlineVerts.Add(v1);
                        outlineVerts.Add(v2);
                        outlineVerts.Add(v2_in);
                        outlineVerts.Add(v1_in);

                        outlineUvs.Add(Vector2.zero);
                        outlineUvs.Add(Vector2.right);
                        outlineUvs.Add(Vector2.one);
                        outlineUvs.Add(Vector2.up);

                        // Double-sided quad
                        outlineTris.Add(bIdx);
                        outlineTris.Add(bIdx + 1);
                        outlineTris.Add(bIdx + 2);

                        outlineTris.Add(bIdx);
                        outlineTris.Add(bIdx + 2);
                        outlineTris.Add(bIdx + 3);

                        outlineTris.Add(bIdx);
                        outlineTris.Add(bIdx + 2);
                        outlineTris.Add(bIdx + 1);

                        outlineTris.Add(bIdx);
                        outlineTris.Add(bIdx + 3);
                        outlineTris.Add(bIdx + 2);
                    }
                }
            }

            var fillMesh = new Mesh();
            fillMesh.name = $"BorderFill_{ownerKey}";
            fillMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            fillMesh.SetVertices(fillVerts);
            fillMesh.SetUVs(0, fillUvs);
            fillMesh.SetTriangles(fillTris, 0);
            fillMesh.RecalculateNormals();
            fillMesh.RecalculateBounds();

            var outlineMesh = new Mesh();
            outlineMesh.name = $"BorderOutline_{ownerKey}";
            outlineMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            outlineMesh.SetVertices(outlineVerts);
            outlineMesh.SetUVs(0, outlineUvs);
            outlineMesh.SetTriangles(outlineTris, 0);
            outlineMesh.RecalculateNormals();
            outlineMesh.RecalculateBounds();

            // Saturated bright outline color for maximum visibility against terrain
            Color outlineColor = new Color(
                Mathf.Min(1f, baseColor.r * 1.35f + 0.1f),
                Mathf.Min(1f, baseColor.g * 1.35f + 0.1f),
                Mathf.Min(1f, baseColor.b * 1.35f + 0.1f),
                0.95f
            );

            Color fillTranslucentColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.42f);

            return new NationRenderBatch
            {
                OwnerKey = ownerKey,
                BaseColor = baseColor,
                FillMesh = fillMesh,
                OutlineMesh = outlineMesh,
                FillMat = SolidColorMaterials.NewSolidColorMaterial(fillTranslucentColor, ShaderDatabase.WorldOverlayTransparent),
                OutlineMat = SolidColorMaterials.NewSolidColorMaterial(outlineColor, ShaderDatabase.WorldOverlayTransparent)
            };
        }
    }

    /// <summary>
    /// Harmony hook to ensure borders are drawn during the official WorldRenderer layer pass.
    /// </summary>
    [HarmonyPatch(typeof(WorldRenderer), "DrawWorldLayers")]
    public static class Patch_DrawWorldLayers_Borders
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                WorldComponent_NationBorders.Instance?.DrawMeshDirect();
            }
            catch
            {
                // Suppress rendering exceptions during initial loading
            }
        }
    }
}
