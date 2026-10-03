using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimNations.Core.GrandStrategy.Compatibility;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimNations.Core.GrandStrategy.Harmony
{
    /// <summary>
    /// Перехватывает движение караванов по глобальной карте мира.
    /// Взимает таможенную пошлину (Customs Tariff) или перехватывает контрабанду.
    /// Полностью совместим с Combat Extended (CE), Vehicles Framework, кастомными фракциями и неофициальными модами.
    /// </summary>
    [HarmonyPatch(typeof(WorldObject))]
    [HarmonyPatch("Tick")]
    public static class Patch_CaravanTransitTax
    {
        private static readonly Dictionary<int, int> caravanLastTiles = new Dictionary<int, int>();
        private static readonly object caravanLock = new object();

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(WorldObject __instance)
        {
            try
            {
                if (!(__instance is Caravan caravan)) return;
                if (!caravan.Spawned) return;

                // Не облагаем пошлиной караваны самого игрока
                if (caravan.Faction == null || caravan.Faction.IsPlayer) return;

                int caravanId = caravan.ID;
                int currentTile = caravan.Tile;

                lock (caravanLock)
                {
                    if (caravanLastTiles.TryGetValue(caravanId, out int oldTile))
                    {
                        if (oldTile == currentTile) return;
                    }
                    caravanLastTiles[caravanId] = currentTile;
                }

                // Проверяем, находится ли клетка в границах нашего государства
                if (IsTileInPlayerTerritory(currentTile))
                {
                    ApplyCustomsToll(caravan, currentTile);
                }
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "Patch_CaravanTransitTax");
            }
        }

        private static bool IsTileInPlayerTerritory(int tile)
        {
            try
            {
                if (RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance != null &&
                    RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance.ClaimedTiles.Count > 0)
                {
                    return RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance.IsTileInPlayerTerritory(tile);
                }

                return false;
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "IsTileInPlayerTerritory");
                return false;
            }
        }

        private static void ApplyCustomsToll(Caravan caravan, int tile)
        {
            try
            {
                var gov = WorldComponent_StateGovernor.Instance;
                if (gov == null) return;

                string factionName = caravan.Faction != null ? caravan.Faction.Name : "Неизвестная фракция";

                // 1. Проверка блокады границы
                if (gov.BorderBlockadeActive)
                {
                    Messages.Message($"[ТАМОЖЕННЫЙ ДОСМОТР] Караван фракции {factionName} задержан на закрытой границе (Клетка {tile})! Контрабанда арестована.",
                        MessageTypeDefOf.ThreatSmall);
                    return;
                }

                // 2. Взимание таможенного тарифа / дани
                float tariff = gov.NationalCustomsTariff;
                if (tariff <= 0.001f) return;

                // Безопасный подсчет стоимости перевозимых товаров через ModCompatibilityManager
                float totalCargoValue = 0f;
                if (caravan.AllThings != null)
                {
                    foreach (var thing in caravan.AllThings)
                    {
                        totalCargoValue += ModCompatibilityManager.SafeGetMarketValue(thing);
                    }
                }

                if (totalCargoValue > 50f)
                {
                    float toll = totalCargoValue * tariff;
                    gov.NationalTreasurySilver += toll;

                    Messages.Message($"[ТАМОЖНЯ] Караван фракции {factionName} пересек государственную границу (Клетка #{tile}) и уплатил {toll:F0} серебра дорожной пошлины в казну!",
                        MessageTypeDefOf.PositiveEvent);

                    // Open interactive border customs modal dialog
                    Find.WindowStack.Add(new RimNations.Core.GrandStrategy.UI.Dialog_BorderCustoms(
                        caravan,
                        tile,
                        totalCargoValue,
                        tariff,
                        "Sovereign Territory"
                    ));
                }
            }
            catch (Exception ex)
            {
                ModCompatibilityManager.HandlePatchException(ex, "ApplyCustomsToll");
            }
        }
    }
}
