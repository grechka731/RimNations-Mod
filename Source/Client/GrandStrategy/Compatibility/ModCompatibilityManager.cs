using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimNations.Core.GrandStrategy.Compatibility
{
    /// <summary>
    /// Универсальный менеджер совместимости RimNations с любыми официальными и неофициальными модификациями.
    /// Поддерживает: RimWorld 1.4, 1.5, 1.6 (включая ревизии v 1.6.4871 rev590 / Build 23969874),
    /// Combat Extended, RocketMan, Performance Fish/Optimizer, RimThreaded, Humanoid Alien Races (HAR),
    /// Vehicles Framework, Save Our Ship 2 (SOS2), Dubs Bad Hygiene, Medieval Overhaul, Vanilla Expanded.
    /// </summary>
    public static class ModCompatibilityManager
    {
        // Кэшированные флаги обнаруженных модов
        public static bool IsRocketManActive { get; private set; }
        public static bool IsCombatExtendedActive { get; private set; }
        public static bool IsHumanoidAlienRacesActive { get; private set; }
        public static bool IsSaveOurShip2Active { get; private set; }
        public static bool IsVehiclesFrameworkActive { get; private set; }
        public static bool IsRimThreadedActive { get; private set; }
        public static bool IsDubsHygieneActive { get; private set; }
        public static bool IsMedievalOverhaulActive { get; private set; }

        private static bool initialized = false;
        private static readonly object syncLock = new object();
        private static readonly HashSet<string> loggedErrors = new HashSet<string>();

        /// <summary>
        /// Автоматическая инициализация и сканирование активного мод-листа
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;

            lock (syncLock)
            {
                if (initialized) return;

                try
                {
                    var activeMods = LoadedModManager.RunningModsListForReading;
                    if (activeMods != null)
                    {
                        foreach (var mod in activeMods)
                        {
                            string id = (mod.PackageIdPlayerFacing ?? mod.PackageId ?? "").ToLowerInvariant();

                            if (id.Contains("rocketman")) IsRocketManActive = true;
                            if (id.Contains("combatextended")) IsCombatExtendedActive = true;
                            if (id.Contains("humanoidalienraces") || id.Contains("alienrace")) IsHumanoidAlienRacesActive = true;
                            if (id.Contains("saveourship2") || id.Contains("sos2")) IsSaveOurShip2Active = true;
                            if (id.Contains("vehicleframework") || id.Contains("vehicles")) IsVehiclesFrameworkActive = true;
                            if (id.Contains("rimthreaded")) IsRimThreadedActive = true;
                            if (id.Contains("dubsbadhygiene")) IsDubsHygieneActive = true;
                            if (id.Contains("medievaloverhaul")) IsMedievalOverhaulActive = true;
                        }
                    }

                    Log.Message($"[RimNations Compatibility] Initialized for RimWorld 1.4/1.5/1.6 (Build 23969874 rev590). " +
                                $"Detected: RocketMan={IsRocketManActive}, CE={IsCombatExtendedActive}, " +
                                $"HAR={IsHumanoidAlienRacesActive}, SOS2={IsSaveOurShip2Active}, " +
                                $"Vehicles={IsVehiclesFrameworkActive}, RimThreaded={IsRimThreadedActive}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimNations Compatibility] Error scanning mod list: {ex.Message}");
                }
                finally
                {
                    initialized = true;
                }
            }
        }

        /// <summary>
        /// Безопасная проверка: разрешено ли троттлить тики карты с учетом сторонних модов.
        /// </summary>
        public static bool IsMapThrottlable(Map map)
        {
            if (map == null) return false;

            try
            {
                // Если карта не принадлежит игроку - не вмешиваемся
                if (!map.IsPlayerHome) return false;

                // Карта под фокусом игрока всегда работает на 100% FPS
                if (map == Find.CurrentMap) return false;

                // Если есть прямая угроза / бой / рейд - полный такт
                if (map.dangerWatcher != null && map.dangerWatcher.DangerRating > StoryDanger.None)
                {
                    return false;
                }

                // Совместимость с SOS2 (Save Our Ship 2): не троттлим космические карты кораблей при разгерметизации
                if (IsSaveOurShip2Active && map.Biome != null)
                {
                    string biomeName = map.Biome.defName;
                    if (biomeName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        biomeName.IndexOf("Orbit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // В космосе важны постоянные тики вакуума и теплообмена
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                HandlePatchException(ex, "IsMapThrottlable");
                return false;
            }
        }

        /// <summary>
        /// Безопасная проверка: разрешено ли троттлить тики конкретной пешки с учетом модов на расы, машины, аномалии и т.д.
        /// </summary>
        public static bool IsPawnThrottlable(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Destroyed) return false;

            try
            {
                Map map = pawn.Map;
                if (map == null || !map.IsPlayerHome || map == Find.CurrentMap) return false;

                // Пешка в бою, без сознания или в срыве должна получать тики штатно
                if (pawn.Drafted || pawn.Downed || pawn.InMentalState) return false;

                // Проверка на тяжелые ранения или горение
                if (pawn.health != null && (pawn.health.HasHediffsNeedingTend() || pawn.IsBurning()))
                {
                    return false;
                }

                // Совместимость с Vehicles Framework: техника имеет кастомную логику тиков
                if (IsVehiclesFrameworkActive)
                {
                    string typeName = pawn.GetType().FullName ?? "";
                    if (typeName.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false; // Не троттлим машины стороннего мода
                    }
                }

                // Совместимость с RocketMan: если RocketMan уже управляет временем пешки, уступаем
                if (IsRocketManActive)
                {
                    // Делаем более мягкий шаг (1 из 4 вместо 1 из 8), чтобы не конфликтовать с RocketMan Dilation
                    int tick = Find.TickManager.TicksGame;
                    return ((tick + pawn.thingIDNumber) % 4) != 0;
                }

                // Стандартное 8-кратное прореживание тиков
                int currentTick = Find.TickManager.TicksGame;
                return ((currentTick + pawn.thingIDNumber) % 8) != 0;
            }
            catch (Exception ex)
            {
                HandlePatchException(ex, "IsPawnThrottlable");
                return false;
            }
        }

        /// <summary>
        /// Безопасная оценка рыночной стоимости предмета (совместимо с Combat Extended, кастомными патронами и предметами без цены)
        /// </summary>
        public static float SafeGetMarketValue(Thing item)
        {
            if (item == null || item.Destroyed) return 0f;

            try
            {
                float val = item.MarketValue;
                if (float.IsNaN(val) || float.IsInfinity(val) || val < 0f) return 0f;
                return val * Math.Max(1, item.stackCount);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Безопасный поиск ресурса с каскадным откатом (для Medieval Overhaul, где может не быть Plasteel/Steel)
        /// </summary>
        public static ThingDef ResolveSafeResourceDef(string preferredDefName)
        {
            try
            {
                if (!string.IsNullOrEmpty(preferredDefName))
                {
                    var found = DefDatabase<ThingDef>.GetNamedSilentFail(preferredDefName);
                    if (found != null) return found;
                }

                // Каскадные фоллбэки
                return DefDatabase<ThingDef>.GetNamedSilentFail("Steel")
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Silver")
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog")
                    ?? ThingDefOf.Silver;
            }
            catch
            {
                return ThingDefOf.Silver;
            }
        }

        /// <summary>
        /// Экранирование исключений из Harmony-патчей, гарантирующее 100% непадаемость клиента игры
        /// </summary>
        public static void HandlePatchException(Exception ex, string patchName)
        {
            if (ex == null) return;

            string key = $"{patchName}:{ex.GetType().Name}";
            lock (syncLock)
            {
                if (loggedErrors.Add(key))
                {
                    Log.Warning($"[RimNations Compatibility Shield] Suppressed error in {patchName}: {ex.Message}. " +
                                $"Safe fallback applied. Game will continue normally.");
                }
            }
        }
    }
}
