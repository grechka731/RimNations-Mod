using System;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Compatibility;
using RimWorld;
using Verse;

namespace RimNations.Core.GrandStrategy.Harmony
{
    /// <summary>
    /// Дросселирование тиков фоновых карт (GovernorMap).
    /// Полностью совместимо с RimWorld 1.4/1.5/1.6 (Build 23969874 rev590) и неофициальными модами.
    /// </summary>
    [HarmonyPatch(typeof(Map))]
    [HarmonyPatch("MapPostTick")]
    public static class Patch_MapTicking
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Map __instance)
        {
            try
            {
                if (!ModCompatibilityManager.IsMapThrottlable(__instance))
                {
                    return true;
                }

                // Для фоновых карт выполняем MapPostTick только 1 раз из 8 тиков
                int tick = Find.TickManager.TicksGame;
                if ((tick % 8) != (__instance.uniqueID & 7))
                {
                    return false; // Пропуск лишней холостой нагрузки
                }

                return true;
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "Patch_MapTicking");
                return true; // При любой непредвиденной ошибке уступаем ванили
            }
        }
    }
}
