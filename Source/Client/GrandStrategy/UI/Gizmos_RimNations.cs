using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    /// <summary>
    /// Worker Gamma: Contextual Town Hall / Building & Settlement Gizmos.
    /// Provides zero-chat-command native interaction to proclaim capitals and assign governors.
    /// </summary>
    public static class Gizmos_RimNations
    {
        public static Command_Action CreateProclaimCapitalGizmo(int tile, string colonyName)
        {
            var gov = WorldComponent_StateGovernor.Instance;
            bool isAlreadyCapital = (gov != null && gov.CapitalTile == tile);

            return new Command_Action()
            {
                defaultLabel = isAlreadyCapital ? "Sovereign Capital" : "Proclaim Capital",
                defaultDesc = isAlreadyCapital
                    ? $"{colonyName} is currently the sovereign Capital (FocusMap) of the realm."
                    : $"Proclaims {colonyName} as the Sovereign Capital of the realm. All macro-yields and tribute flow here.",
                icon = BaseContent.BadTex,
                action = () =>
                {
                    if (gov != null)
                    {
                        gov.SetCapital(tile);
                        Messages.Message($"[RIMNATIONS] {colonyName} proclaimed as Sovereign Capital!", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };
        }

        public static Command_Action CreateAssignGovernorGizmo(Map map, int tile, string colonyName)
        {
            var gov = WorldComponent_StateGovernor.Instance;
            string currentGovId = gov?.GetGovernorId(tile);
            Pawn currentGov = map?.mapPawns.FreeColonists.FirstOrDefault(p => p.ThingID == currentGovId);

            return new Command_Action()
            {
                defaultLabel = currentGov != null ? $"Gov: {currentGov.LabelShort}" : "Assign Governor",
                defaultDesc = "Appoint a provincial governor from the local colonists to administer this territory and manage unrest.",
                icon = BaseContent.BadTex,
                action = () =>
                {
                    if (map == null) return;
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("[Dismiss Governor]", () =>
                    {
                        gov?.SetGovernor(tile, null);
                        Messages.Message($"[RIMNATIONS] Governor dismissed from {colonyName}.", MessageTypeDefOf.NeutralEvent);
                    }));

                    foreach (var p in map.mapPawns.FreeColonists)
                    {
                        var colonist = p;
                        int soc = colonist.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                        string traitNotice = "";
                        if (colonist.story?.traits != null)
                        {
                            if (colonist.story.traits.HasTrait(TraitDefOf.Greedy)) traitNotice += " [Greedy: +20% output, +1.5% unrest]";
                            if (colonist.story.traits.HasTrait(TraitDefOf.Kind)) traitNotice += " [Kind: -1.0% unrest]";
                            if (colonist.story.traits.HasTrait(TraitDefOf.Abrasive)) traitNotice += " [Abrasive: +2.0% unrest]";
                        }

                        options.Add(new FloatMenuOption($"{colonist.LabelShort} (Social {soc}){traitNotice}", () =>
                        {
                            gov?.SetGovernor(tile, colonist.ThingID);
                            Messages.Message($"[RIMNATIONS] {colonist.LabelShort} appointed Governor of {colonyName}.", MessageTypeDefOf.TaskCompletion);
                        }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        public static Command_Action CreateOpenDashboardGizmo()
        {
            return new Command_Action()
            {
                defaultLabel = "RimNations Dashboard",
                defaultDesc = "Open the central geopolitical MMO & grand strategy management interface (Hotkey 'U').",
                icon = BaseContent.BadTex,
                action = () =>
                {
                    if (!Find.WindowStack.IsOpen<Dialog_RimNationsDashboard>())
                    {
                        Find.WindowStack.Add(new Dialog_RimNationsDashboard());
                    }
                }
            };
        }

        [HarmonyPatch(typeof(Settlement))]
        [HarmonyPatch("GetGizmos")]
        public static class Patch_SettlementGizmos
        {
            [HarmonyPostfix]
            public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
            {
                foreach (var g in __result)
                {
                    yield return g;
                }

                if (__instance != null && __instance.Faction != null && __instance.Faction.IsPlayer)
                {
                    yield return CreateOpenDashboardGizmo();
                    yield return CreateProclaimCapitalGizmo(__instance.Tile, __instance.LabelCap);

                    Map map = __instance.Map;
                    if (map != null)
                    {
                        yield return CreateAssignGovernorGizmo(map, __instance.Tile, __instance.LabelCap);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Building_CommsConsole))]
        [HarmonyPatch("GetGizmos")]
        public static class Patch_CommsConsoleGizmos
        {
            [HarmonyPostfix]
            public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_CommsConsole __instance)
            {
                foreach (var g in __result)
                {
                    yield return g;
                }

                if (__instance != null && __instance.Faction != null && __instance.Faction.IsPlayer && __instance.Map != null)
                {
                    yield return CreateOpenDashboardGizmo();
                    yield return CreateProclaimCapitalGizmo(__instance.Map.Tile, __instance.Map.info?.parent?.LabelCap ?? "Colony");
                    yield return CreateAssignGovernorGizmo(__instance.Map, __instance.Map.Tile, __instance.Map.info?.parent?.LabelCap ?? "Colony");
                }
            }
        }
    }
}
