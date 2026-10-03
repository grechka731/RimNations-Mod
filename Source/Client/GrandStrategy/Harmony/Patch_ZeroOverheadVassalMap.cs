using System;
using System.Linq;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Compatibility;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimNations.Core.GrandStrategy.Harmony
{
    /// <summary>
    /// Worker Alpha: Zero-overhead background simulation for inactive vassal maps (GovernorMap).
    /// Suppresses map rendering (MapDrawer), audio (SoundStarter), work pathfinding (JobGiver_Work),
    /// and enforces contiguous expansion limits on world map settlement.
    /// </summary>
    public static class Patch_ZeroOverheadVassalMap
    {
        [HarmonyPatch(typeof(JobGiver_Work))]
        [HarmonyPatch("TryIssueJobPackage")]
        public static class Patch_JobGiver_Work
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool Prefix(Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
            {
                try
                {
                    if (pawn == null || !pawn.Spawned || pawn.Dead) return true;
                    Map map = pawn.Map;
                    if (map == null || map == Find.CurrentMap || !map.IsPlayerHome) return true;

                    // If drafted or in active combat/danger, let pawn handle jobs normally
                    if (pawn.Drafted || (map.dangerWatcher != null && map.dangerWatcher.DangerRating > StoryDanger.None))
                    {
                        return true;
                    }

                    // Suppress heavy work job allocation on inactive vassal maps: macro-engine handles yields!
                    __result = ThinkResult.NoJob;
                    return false;
                }
                catch (Exception ex)
                {
                    ModCompatibilityManager.HandlePatchException(ex, "Patch_JobGiver_Work");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SoundStarter))]
        [HarmonyPatch("PlayOneShot", new Type[] { typeof(SoundDef), typeof(SoundInfo) })]
        public static class Patch_SoundStarter
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool Prefix(SoundDef soundDef, SoundInfo info)
            {
                try
                {
                    // If sound is originating from an inactive map, suppress audio playback
                    Map soundMap = info.Maker.Map;
                    if (soundMap != null && soundMap != Find.CurrentMap && soundMap.IsPlayerHome)
                    {
                        return false; // Suppress background vassal sounds
                    }
                    return true;
                }
                catch
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Map))]
        [HarmonyPatch("MapUpdate")]
        public static class Patch_MapUpdate
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool Prefix(Map __instance)
            {
                try
                {
                    if (__instance != null && __instance != Find.CurrentMap && __instance.IsPlayerHome)
                    {
                        return false; // Suppress rendering and frame updates on non-focus maps
                    }
                    return true;
                }
                catch
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SettleInEmptyTileUtility))]
        [HarmonyPatch("Settle")]
        public static class Patch_CaravanSettleContiguity
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool Prefix(Caravan caravan)
            {
                try
                {
                    if (caravan == null || Find.WorldObjects == null || Find.WorldGrid == null) return true;

                    int tile = caravan.Tile;
                    var playerSettlements = Find.WorldObjects.Settlements
                        .Where(s => s != null && s.Faction != null && s.Faction.IsPlayer)
                        .ToList();

                    // If this is the player's first settlement, allow anywhere valid
                    if (playerSettlements.Count == 0) return true;

                    // Contiguous expansion doctrine: new settlements must be adjacent to existing national borders
                    var gov = WorldComponent_StateGovernor.Instance;
                    if (gov != null && !gov.IsTileAdjacentToBorder(tile, 2))
                    {
                        Messages.Message("Нельзя основать колонию: новая территория должна прилегать к существующей границе государства! Анклавы запрещены доктриной RimNations.",
                            MessageTypeDefOf.RejectInput);
                        return false; // Block founding disconnected enclave
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    ModCompatibilityManager.HandlePatchException(ex, "Patch_CaravanSettleContiguity");
                    return true;
                }
            }
        }
    }
}
