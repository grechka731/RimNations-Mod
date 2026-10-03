using System;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Compatibility;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimNations.Core.GrandStrategy.Harmony
{
    /// <summary>
    /// Перехватывает индивидуальные срывы колонистов (MentalBreakWorker.TryStart).
    /// Вместо хаотичных истерик отдельных пешек (съел без стола, сломал компонент),
    /// недовольство аккумулируется в общее Unrest (недовольство) города.
    /// Полностью безопасно для Humanoid Alien Races, дроидов и существ из DLC Anomaly.
    /// </summary>
    [HarmonyPatch(typeof(MentalBreakWorker))]
    [HarmonyPatch("TryStart")]
    public static class Patch_MentalStateInterception
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        public static bool Prefix(MentalBreakWorker __instance, Pawn pawn, string reason, bool causedByMood, ref bool __result)
        {
            try
            {
                if (pawn == null || !pawn.IsColonist || pawn.Map == null) return true;

                // На фокусной карте сохраняются стандартные ванильные срывы под надзором игрока
                if (pawn.Map == Find.CurrentMap) return true;

                // Если существо не имеет системы настроения (дроиды, механоиды, мутанты без разума) - не трогаем
                if (pawn.needs == null || pawn.needs.mood == null) return true;

                // Если срыв вызван настроением
                if (causedByMood)
                {
                    var govComp = WorldComponent_StateGovernor.Instance;
                    if (govComp != null)
                    {
                        string colonyName = pawn.Map.info?.parent?.LabelCap ?? "Поселение";
                        var tracker = govComp.GetOrCreateStabilityTracker(pawn.Map.Tile, colonyName);

                        float grievanceAmount = 5.0f;
                        if (__instance.def != null)
                        {
                            switch (__instance.def.intensity)
                            {
                                case MentalBreakIntensity.Minor:
                                    grievanceAmount = 5.0f;
                                    break;
                                case MentalBreakIntensity.Major:
                                    grievanceAmount = 15.0f;
                                    break;
                                case MentalBreakIntensity.Extreme:
                                    grievanceAmount = 30.0f;
                                    break;
                            }
                        }

                        string cause = string.IsNullOrEmpty(reason) ? "Лишения и тяготы службы" : reason;
                        tracker.AddGrievance(grievanceAmount, $"{pawn.LabelShort} ({__instance.def?.intensity}): {cause}");

                        // Даем катарсис для снятия острого пика стресса
                        try
                        {
                            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.Catharsis);
                        }
                        catch
                        {
                            // Безопасное подавление, если у кастомной расы нет слота памяти
                        }

                        __result = false;
                        return false; // Отменяем ванильный нервный срыв
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "Patch_MentalStateInterception");
                return true; // При любой ошибке не крашим игру
            }
        }
    }
}
