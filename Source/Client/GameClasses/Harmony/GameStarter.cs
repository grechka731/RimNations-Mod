using HarmonyLib;
using OCUnion;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine.SceneManagement;
using Verse;

namespace RimWorldOnlineCity
{
    public class GameStarter
    {
        public static Scenario SetScenario = null;
        public static string SetScenarioName = null;
        public static int SetMapSize = 0;
        public static float SetPlanetCoverage = 0;
        public static string SetSeed = null;
        public static OverallRainfall SetOverallRainfall = OverallRainfall.Normal;
        public static OverallTemperature SetOverallTemperature = OverallTemperature.Normal;
        public static string SetDifficulty = null;
        public static Action AfterStart = null;
        public static List<Pawn> SetPawns = null;
        //public static bool DisableAllRandom = false;

        public static void GoToMainMenu()
        {
            //надо? to do
            SceneManager.LoadScene("Entry");
        }

        public static void GameGeneration(bool withStart = true)
        {
            var quickStarterType = typeof(Root).Assembly.GetType("Verse.QuickStarter");
            if (quickStarterType == null)
            {
                Loger.Log("Client Verse.QuickStarter type not found");
                return;
            }
            var quickStartedField = AccessTools.Field(quickStarterType, "quickStarted");
            if (quickStartedField == null)
            {
                Loger.Log("Client QuickStarter.quickStarted field not found");
                return;
            }

            quickStartedField.SetValue(null, true);

            if (withStart)
                LongEventHandler.QueueLongEvent(() => {
                    Current.Game = null;
                }, "Play", "GeneratingMap", true, null);
        }
        
        internal static Scenario ReplaceQuickstartScenarioIfNeeded(Scenario original)
        {
            return SetScenario ?? original;
        }

        internal static int ReplaceQuickstartMapSizeIfNeeded(int original)
        {
            return SetMapSize > 0 ? SetMapSize : original;
        }

        //это не работает, т.к. WorldGenerator.GenerateWorld должен запускаться только 1 раз (он как-то уже привязывается к пешкам)
        //поэтому патчим сам этот метод
        internal static World ReplaceQuickstartWorldIfNeeded(World world)
        {
            return world;
            //return WorldGenerator.GenerateWorld(0.05f, "12345"/*GenText.RandomSeedString()*/, OverallRainfall.Normal, OverallTemperature.Normal);
        }

    }

    /// ////////////////////////////////////////////////////////////
    
    //Дополняем проверку на место для нового поселения
    [HarmonyPatch(typeof(TileFinder))]
    [HarmonyPatch("IsValidTileForNewSettlement")]
    internal class TileFinder_IsValidTileForNewSettlement_Patch
    {
        public static bool Off = false;
        
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, object[] __args)
        {
            if (Off) return;
            if (!__result) return;
            if (__args == null || __args.Length == 0 || __args[0] == null) return;

            int tileId = 0;
            object firstArg = __args[0];
            if (firstArg is int tInt)
            {
                tileId = tInt;
            }
            else
            {
                var fi = firstArg.GetType().GetField("tileId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fi != null)
                {
                    tileId = (int)fi.GetValue(firstArg);
                }
                else
                {
                    tileId = Convert.ToInt32(firstArg);
                }
            }

            StringBuilder reason = __args.Length > 1 ? __args[1] as StringBuilder : null;
            WorldGrid worldGrid = Find.WorldGrid;
            var listWO = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < listWO.Count; i++)
            {
                if (!(listWO[i] is BaseOnline)) continue;
                var wot = listWO[i].Tile;
                if (wot == tileId || worldGrid.IsNeighborOrSame(wot, tileId))
                {
                    if (reason != null)
                    {
                        reason.Append("OCity_Starter_CityNotBuild".Translate());
                    }
                    __result = false;
                    return;
                }
            }

            // RIMNATIONS TERRITORIAL VALIDATION:
            // 1. Сгенерированная органическая территория (10-13 для столицы, 7-10 для города) НЕ должна накладываться на чужие границы
            // 2. Новый город обязан прилегать к существующей границе своего государства
            try
            {
                if (Current.Game != null && Find.WorldObjects != null)
                {
                    var mySettlements = Find.WorldObjects.Settlements
                        .Where(s => s.Faction != null && s.Faction.IsPlayer)
                        .ToList();

                    bool isCapital = (mySettlements.Count == 0);
                    var proposedTiles = RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.GenerateOrganicTerritory(tileId, isCapital);

                    // 1. Проверяем наложение на суверенные территории других игроков
                    var otherBases = Find.WorldObjects.AllWorldObjects?.OfType<BaseOnline>().ToList();
                    string myLogin = SessionClientController.My?.Login;

                    if (otherBases != null && otherBases.Count > 0)
                    {
                        var foreignTiles = new HashSet<int>();
                        foreach (var b in otherBases)
                        {
                            if (b.OnlineWObject == null) continue;
                            if (!string.IsNullOrEmpty(myLogin) && b.OnlineWObject.LoginOwner == myLogin) continue;

                            bool fbIsCap = b.OnlineWObject.IsCapital;
                            var bClaim = RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.GenerateOrganicTerritory(b.Tile, fbIsCap);
                            foreach (int t in bClaim) foreignTiles.Add(t);
                        }

                        foreach (int pt in proposedTiles)
                        {
                            if (foreignTiles.Contains(pt))
                            {
                                if (reason != null)
                                {
                                    reason.Append("Территория поселения пересекается с суверенными границами другого государства!");
                                }
                                __result = false;
                                return;
                            }
                        }
                    }

                    // 2. Для вторичных городов проверяем неразрывность с границами своего государства
                    if (!isCapital)
                    {
                        var myCurrentTiles = new HashSet<int>();
                        var gov = RimNations.Core.GrandStrategy.Core.WorldComponent_StateGovernor.Instance;
                        foreach (var s in mySettlements)
                        {
                            bool sCap = (gov != null && gov.CapitalTile == s.Tile);
                            var sClaim = RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.GenerateOrganicTerritory(s.Tile, sCap);
                            foreach (int t in sClaim) myCurrentTiles.Add(t);
                        }

                        bool touchesOwnBorders = false;
                        var tempNeighbors = new List<PlanetTile>();
                        foreach (int pt in proposedTiles)
                        {
                            if (myCurrentTiles.Contains(pt))
                            {
                                touchesOwnBorders = true;
                                break;
                            }
                            tempNeighbors.Clear();
                            worldGrid.GetTileNeighbors((PlanetTile)pt, tempNeighbors);
                            if (tempNeighbors.Any(n => myCurrentTiles.Contains(n.tileId)))
                            {
                                touchesOwnBorders = true;
                                break;
                            }
                        }

                        if (!touchesOwnBorders)
                        {
                            if (reason != null)
                            {
                                reason.Append("Новый город должен прилегать к существующей границе вашего государства!");
                            }
                            __result = false;
                            return;
                        }
                    }
                }
            }
            catch { }
        }
    }

    /// ////////////////////////////////////////////////////////////

    //Устанавливаем параметры при генерации мира
    [HarmonyPatch(typeof(WorldGenerator))]
    [HarmonyPatch("GenerateWorld")]
    //[HarmonyPatch(new[] { typeof(float), typeof(string), typeof(OverallRainfall), typeof(OverallTemperature) })]
    internal class WorldGenerator_GenerateWorld_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref float planetCoverage, ref string seedString, ref OverallRainfall overallRainfall, ref OverallTemperature overallTemperature)
        {
            Loger.Log("Client HarmonyPatch WorldGenerator.GenerateWorld()");
            //параметры генерации мира
            if (GameStarter.SetPlanetCoverage > 0) planetCoverage = GameStarter.SetPlanetCoverage;
            if (!string.IsNullOrEmpty(GameStarter.SetSeed)) seedString = GameStarter.SetSeed;
            if (GameStarter.SetPlanetCoverage > 0 || !string.IsNullOrEmpty(GameStarter.SetSeed))
            {
                overallRainfall = GameStarter.SetOverallRainfall;
                overallTemperature = GameStarter.SetOverallTemperature;
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            //устанавливаем пешки
            if (GameStarter.SetPawns != null)
            {
                Current.Game.InitData.startingAndOptionalPawns = GameStarter.SetPawns;
            }
        }
    }
    
    //событие когда игра готова
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("InitNewGame")]
    internal class Game_InitNewGame_Patch
    {

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (GameStarter.AfterStart != null)
            {
                Loger.Log("Client HarmonyPatch Game.InitNewGame()");
                GameStarter.AfterStart();
            }
        }

    }

    //Устанавливаем параметры при генерации мира и событие готовности для запуска через SetupForQuickTestPlay (сейчас не используется)
    [HarmonyPatch(typeof(Root_Play))]
    [HarmonyPatch("SetupForQuickTestPlay")]
    internal class RootPlay_TestPlay_Patch
    {
        private static bool patchedScenario;
        private static bool patchedSize;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (GameStarter.AfterStart != null)
            {
                Loger.Log("Client HarmonyPatch Root_Play.SetupForQuickTestPlay()");
                GameStarter.AfterStart();
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InjectCustomQuickstartSettings(IEnumerable<CodeInstruction> instructions)
        {
            var gameSetScenarioMethod = AccessTools.Method(typeof(Game), "set_Scenario");
            var gameInitDataMapSizeField = AccessTools.Field(typeof(GameInitData), "mapSize");
            var gameSetWorldMethod = AccessTools.Method(typeof(Game), "set_World");
            if (gameSetScenarioMethod == null || gameInitDataMapSizeField == null || gameSetWorldMethod == null)
            {
                Loger.Log("Client Failed to reflect a required member: " + Environment.StackTrace);
            }
            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Callvirt && Equals(inst.operand, gameSetWorldMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call
                        , ((Func<World, World>)GameStarter.ReplaceQuickstartWorldIfNeeded).Method);
                }
                else
                if (inst.opcode == OpCodes.Callvirt && Equals(inst.operand, gameSetScenarioMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call
                        , ((Func<Scenario, Scenario>)GameStarter.ReplaceQuickstartScenarioIfNeeded).Method);
                    patchedScenario = true;
                }
                else if (inst.opcode == OpCodes.Stfld && Equals(inst.operand, gameInitDataMapSizeField))
                {
                    yield return new CodeInstruction(OpCodes.Call
                        , ((Func<int, int>)GameStarter.ReplaceQuickstartMapSizeIfNeeded).Method);
                    patchedSize = true;
                }
                yield return inst;
                
            }
        }
    }

    /// ////////////////////////////////////////////////////////////
    
    //Устанавливаем параметры при генерации мира
    [HarmonyPatch(typeof(Autosaver))]
    [HarmonyPatch("DoAutosave")]
    internal class Autosaver_DoAutosave_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SessionClient.Get.IsLogined)
            {
                Loger.Log("Client HarmonyPatch Autosaver.DoAutosave cancel");
                return false;
            }
            else
                return true;
        }
    }

}
