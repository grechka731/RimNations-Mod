using System;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Compatibility;
using RimWorld;
using Verse;

namespace RimNations.Core.GrandStrategy.Harmony
{
    /// <summary>
    /// Оптимизация тиков фоновых пешек (Bypassing Micro-Job / Pathfinding Saturation).
    /// Поддерживает совместимость с RocketMan, Vehicles Framework, HAR (Alien Races), Biotech Xenotypes, Anomaly Entities.
    /// </summary>
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("Tick")]
    public static class Patch_PawnTicking
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Pawn __instance)
        {
            try
            {
                if (!ModCompatibilityManager.IsPawnThrottlable(__instance))
                {
                    // Active execution cycle: compensate needs for the 7 throttled ticks
                    CompensateThrottledNeeds(__instance);
                    return true;
                }

                // Skipped tick: suppress processing
                return false;
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "Patch_PawnTicking");
                return true; // 100% safe fallback to vanilla ticking
            }
        }

        private static void CompensateThrottledNeeds(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.needs == null) return;
            Map map = pawn.Map;
            if (map == null || map == Find.CurrentMap || !map.IsPlayerHome) return;

            const int skippedTicks = 7;
            try
            {
                if (pawn.needs.food != null)
                {
                    float foodLoss = pawn.needs.food.FoodFallPerTick * skippedTicks;
                    pawn.needs.food.CurLevel = Math.Max(0f, pawn.needs.food.CurLevel - foodLoss);
                }

                if (pawn.needs.rest != null)
                {
                    if (pawn.InBed())
                    {
                        pawn.needs.rest.CurLevel = Math.Min(pawn.needs.rest.MaxLevel, pawn.needs.rest.CurLevel + 0.00055f * skippedTicks);
                    }
                    else
                    {
                        float restLoss = pawn.needs.rest.RestFallPerTick * skippedTicks;
                        pawn.needs.rest.CurLevel = Math.Max(0f, pawn.needs.rest.CurLevel - restLoss);
                    }
                }
            }
            catch
            {
                // Silently safeguard against non-standard pawn races or mechanoids
            }
        }
    }
}
