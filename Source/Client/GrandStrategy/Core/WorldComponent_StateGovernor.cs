using System;
using System.Collections.Generic;
using System.Linq;
using RimNations.Core.GrandStrategy.Economics;
using RimNations.Core.GrandStrategy.Stability;
using RimWorld;
using RimWorld.Planet;
using RimWorldOnlineCity;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.Core
{
    public class WorldComponent_StateGovernor : WorldComponent
    {
        public static WorldComponent_StateGovernor Instance { get; private set; }

        public float NationalCustomsTariff = 0.05f; // 5% default tariff
        public float NationalTreasurySilver = 1000f;
        public bool BorderBlockadeActive = false;
        public bool CustomColorSet = false;
        public Color NationalColor = new Color(0.18f, 0.55f, 0.95f, 0.45f);

        public GovernmentType GovType = GovernmentType.Republic;
        public List<MinistryOffice> MinistryOffices = new List<MinistryOffice>();
        public List<CitizenRecord> Citizens = new List<CitizenRecord>();
        public List<MarketTradeLot> MarketLots = new List<MarketTradeLot>();
        public List<MercenaryPlatoonContract> MercenaryContracts = new List<MercenaryPlatoonContract>();
        public List<SovereignLoanContract> LoanContracts = new List<SovereignLoanContract>();

        // 16 curated high-contrast base hue anchors evenly distributed in HSV space
        private static readonly float[] MasterBaseHues = new float[]
        {
            0.555f, // Sky Blue
            0.402f, // Emerald Green
            0.133f, // Golden Amber
            0.777f, // Electric Violet
            0.486f, // Vivid Teal
            0.078f, // Tangerine Orange
            0.875f, // Magenta / Fuchsia
            0.236f, // Chartreuse
            0.680f, // Deep Indigo
            0.042f, // Ruby Coral
            0.444f, // Mint Turquoise
            0.736f, // Royal Purple
            0.152f, // Bright Goldenrod
            0.514f, // Neon Cyan
            0.930f, // Berry Rose
            0.305f  // Lime Green
        };

        /// <summary>
        /// Deterministically computes a unique, vibrant, maximally distinct nation color based on player login.
        /// Uses 16 curated HSV hue anchors with fine FNV-1a hashing to guarantee high visual separation.
        /// </summary>
        public static Color GetColorFromLogin(string login, float alpha = 0.50f)
        {
            if (string.IsNullOrEmpty(login)) return new Color(0.20f, 0.60f, 0.95f, alpha);

            uint hash = 2166136261;
            for (int i = 0; i < login.Length; i++)
            {
                hash ^= (byte)login[i];
                hash *= 16777619;
            }

            int slot = (int)(hash % (uint)MasterBaseHues.Length);
            float baseHue = MasterBaseHues[slot];

            // Fine sub-offset using middle bits to prevent collisions while preserving anchor slot
            float hueShift = (((hash >> 8) % 100) / 100f - 0.5f) * 0.04f;
            float hue = Mathf.Repeat(baseHue + hueShift, 1.0f);

            // High saturation 0.80..0.94 and brightness 0.88..0.98 for vivid border visibility on 3D globe
            float sat = 0.80f + ((hash >> 16) % 15) / 100f;
            float val = 0.88f + ((hash >> 24) % 11) / 100f;

            Color rgb = Color.HSVToRGB(hue, sat, val);
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        public Color GetEffectiveColor()
        {
            string login = SessionClientController.My?.Login ?? "Local";
            return GetColorFromLogin(login, 0.50f);
        }

        // Tile -> SettlementStabilityTracker
        private Dictionary<int, SettlementStabilityTracker> stabilityTrackers = new Dictionary<int, SettlementStabilityTracker>();

        // Tile -> Archetype DefName
        private Dictionary<int, string> settlementArchetypes = new Dictionary<int, string>();

        // Tile -> Governor Pawn ThingID
        private Dictionary<int, string> settlementGovernors = new Dictionary<int, string>();

        // Accumulated abstract resources awaiting physical deposit: Tile -> DefName -> Amount
        private Dictionary<int, Dictionary<string, float>> pendingYields = new Dictionary<int, Dictionary<string, float>>();

        private int macroTickCounter = 0;
        public const int MacroTickInterval = 300; // ~5 seconds of game time at 60 TPS

        public WorldComponent_StateGovernor(World world) : base(world)
        {
            Instance = this;
            InitializeDefaultStateData();
        }


        public void InitializeDefaultStateData()
        {
            if (MinistryOffices == null) MinistryOffices = new List<MinistryOffice>();
            if (MinistryOffices.Count == 0)
            {
                MinistryOffices.Add(new MinistryOffice("Grand Chancellor", SessionClientController.My?.Login ?? "")
                {
                    CanDeclareWar = true,
                    CanAuthorizePeace = true,
                    CanInviteBanishCitizens = true,
                    CanAdjustCustomsTariff = true,
                    CanAccessNationalTreasury = true
                });
                MinistryOffices.Add(new MinistryOffice("Minister of War")
                {
                    CanDeclareWar = true
                });
                MinistryOffices.Add(new MinistryOffice("Minister of Trade & Treasury")
                {
                    CanAdjustCustomsTariff = true,
                    CanAccessNationalTreasury = true
                });
                MinistryOffices.Add(new MinistryOffice("Diplomatic Emissary")
                {
                    CanAuthorizePeace = true,
                    CanInviteBanishCitizens = true
                });
            }

            if (Citizens == null) Citizens = new List<CitizenRecord>();
            if (Citizens.Count == 0)
            {
                string myLogin = SessionClientController.My?.Login ?? "Sovereign";
                Citizens.Add(new CitizenRecord(myLogin, "Head of State", 1));
            }

            if (MarketLots == null) MarketLots = new List<MarketTradeLot>();
            if (MarketLots.Count == 0)
            {
                MarketLots.Add(new MarketTradeLot("Orbital Consortium", "Core Worlds", "Steel", "Steel", 250, 1.9f, MarketCategory.Resources));
                MarketLots.Add(new MarketTradeLot("Biotech Syndicate", "Horizon League", "MedicineIndustrial", "Medicine", 30, 16.5f, MarketCategory.Medicine));
                MarketLots.Add(new MarketTradeLot("Imperial Machineforge", "Iron Bastion", "ComponentIndustrial", "Industrial Component", 15, 32.0f, MarketCategory.TechArtifacts));
                MarketLots.Add(new MarketTradeLot("Frontier Guild", "Free Colonies", "Plasteel", "Plasteel", 60, 9.2f, MarketCategory.Resources));
                MarketLots.Add(new MarketTradeLot("Agri-Corp Union", "Verdant Reach", "RawPotatoes", "Raw Potatoes", 500, 1.1f, MarketCategory.Food));
            }

            if (MercenaryContracts == null) MercenaryContracts = new List<MercenaryPlatoonContract>();
            if (MercenaryContracts.Count == 0)
            {
                MercenaryContracts.Add(new MercenaryPlatoonContract("Fortress Command", "Iron March", "4th Heavy Assault Platoon", 4, 150f, 5));
                MercenaryContracts.Add(new MercenaryPlatoonContract("Ghost Directorate", "Shadow Syndicate", "Spectre Recon Squad", 3, 120f, 7));
                MercenaryContracts.Add(new MercenaryPlatoonContract("Aegis Legion", "Gilded Coalition", "Vanguard Defense Phalanx", 5, 200f, 4));
            }

            if (LoanContracts == null) LoanContracts = new List<SovereignLoanContract>();
            if (LoanContracts.Count == 0)
            {
                LoanContracts.Add(new SovereignLoanContract("Imperial Banking Syndicate", 5000f, 0.08f, -1, "Designated Province"));
                LoanContracts.Add(new SovereignLoanContract("Interstellar Credit Union", 10000f, 0.12f, -1, "Designated Province"));
                LoanContracts.Add(new SovereignLoanContract("Frontier Merchant Bank", 3000f, 0.05f, -1, "Designated Province"));
            }
        }

        public bool HasPermission(string login, MinistryPermission permission)
        {
            if (string.IsNullOrEmpty(login))
            {
                login = SessionClientController.My?.Login ?? "Local";
            }
            string leaderLogin = SessionClientController.My?.Login ?? "Local";
            if (login.Equals(leaderLogin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var assignedOffices = MinistryOffices.Where(o => !string.IsNullOrEmpty(o.AssignedPlayerLogin) && o.AssignedPlayerLogin.Equals(login, StringComparison.OrdinalIgnoreCase));
            foreach (var office in assignedOffices)
            {
                switch (permission)
                {
                    case MinistryPermission.DeclareWar:
                        if (office.CanDeclareWar) return true;
                        break;
                    case MinistryPermission.AuthorizePeace:
                        if (office.CanAuthorizePeace) return true;
                        break;
                    case MinistryPermission.InviteBanishCitizens:
                        if (office.CanInviteBanishCitizens) return true;
                        break;
                    case MinistryPermission.AdjustCustomsTariff:
                        if (office.CanAdjustCustomsTariff) return true;
                        break;
                    case MinistryPermission.AccessNationalTreasury:
                        if (office.CanAccessNationalTreasury) return true;
                        break;
                }
            }
            return false;
        }

        public bool DispatchImperialInspector(int tile)
        {
            if (NationalTreasurySilver < 100f)
            {
                Messages.Message("[RIMNATIONS] Insufficient national treasury funds! Dispatching an Imperial Inspector requires 100 Silver.", MessageTypeDefOf.RejectInput);
                return false;
            }

            NationalTreasurySilver -= 100f;
            var tracker = GetOrCreateStabilityTracker(tile, $"Province #{tile}");
            float previousUnrest = tracker.Unrest;
            tracker.Unrest = Mathf.Max(0f, tracker.Unrest - 25f);

            int embezzledRecovered = Rand.Range(40, 120);
            NationalTreasurySilver += embezzledRecovered;

            Messages.Message($"[IMPERIAL INSPECTOR] Emissary circuit completed in province #{tile}! Unrest reduced from {previousUnrest:F1}% to {tracker.Unrest:F1}%. Exposed and seized {embezzledRecovered} Silver in embezzled colonial revenue.", MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool DepositSilverToTreasury(Map map, float amount)
        {
            if (map == null || amount <= 0) return false;
            int needed = Mathf.FloorToInt(amount);

            var silverThings = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => !t.Position.Fogged(map) && t.stackCount > 0).ToList();
            int available = silverThings.Sum(t => t.stackCount);
            if (available < needed)
            {
                Messages.Message($"[TREASURY] Not enough physical silver in stockpiles! Available: {available}, Required: {needed}.", MessageTypeDefOf.RejectInput);
                return false;
            }

            int remaining = needed;
            foreach (var st in silverThings)
            {
                if (remaining <= 0) break;
                int take = Math.Min(st.stackCount, remaining);
                st.SplitOff(take).Destroy();
                remaining -= take;
            }

            NationalTreasurySilver += needed;
            Messages.Message($"[TREASURY] Deposited {needed} Silver into the Sovereign National Treasury. Balance: {NationalTreasurySilver:F0} Silver.", MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool WithdrawSilverFromTreasury(Map map, float amount)
        {
            if (map == null || amount <= 0) return false;
            int requested = Mathf.FloorToInt(amount);

            if (NationalTreasurySilver < requested)
            {
                Messages.Message($"[TREASURY] Insufficient funds in national treasury! Available: {NationalTreasurySilver:F0}, Requested: {requested}.", MessageTypeDefOf.RejectInput);
                return false;
            }

            NationalTreasurySilver -= requested;

            IntVec3 dropCell = FindBestDepositCell(map, ThingDefOf.Silver);
            int toSpawn = requested;
            int stackLimit = ThingDefOf.Silver.stackLimit;
            while (toSpawn > 0)
            {
                int batch = Math.Min(toSpawn, stackLimit);
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = batch;
                GenPlace.TryPlaceThing(silver, dropCell, map, ThingPlaceMode.Near);
                toSpawn -= batch;
            }

            Messages.Message($"[TREASURY] Withdrew {requested} Silver from Sovereign National Treasury to local colony stockpiles. Remaining: {NationalTreasurySilver:F0} Silver.", MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool PurchaseMarketLot(MarketTradeLot lot, bool expressDropPod, Map targetMap)
        {
            if (lot == null) return false;
            targetMap = targetMap ?? Find.CurrentMap;
            if (targetMap == null)
            {
                Messages.Message("[MARKETPLACE] No valid target colony map available for delivery.", MessageTypeDefOf.RejectInput);
                return false;
            }

            float fee = expressDropPod ? (lot.TotalPrice * 0.10f) : 0f;
            float totalCost = lot.TotalPrice + fee;

            if (NationalTreasurySilver < totalCost)
            {
                Messages.Message($"[MARKETPLACE] Insufficient Treasury Silver! Total required: {totalCost:F0} Silver (Balance: {NationalTreasurySilver:F0}).", MessageTypeDefOf.RejectInput);
                return false;
            }

            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(lot.ItemDefName);
            if (thingDef == null)
            {
                Messages.Message($"[MARKETPLACE] Error: ThingDef '{lot.ItemDefName}' not recognized.", MessageTypeDefOf.RejectInput);
                return false;
            }

            NationalTreasurySilver -= totalCost;
            MarketLots.Remove(lot);

            List<Thing> deliveredThings = new List<Thing>();
            int remainingCount = lot.StackCount;
            int stackLimit = Math.Max(1, thingDef.stackLimit);

            while (remainingCount > 0)
            {
                int batch = Math.Min(remainingCount, stackLimit);
                Thing item = ThingMaker.MakeThing(thingDef);
                item.stackCount = batch;
                if (item.TryGetComp<CompQuality>() is CompQuality compQ)
                {
                    compQ.SetQuality(lot.Quality, ArtGenerationContext.Colony);
                }
                deliveredThings.Add(item);
                remainingCount -= batch;
            }

            if (expressDropPod)
            {
                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(targetMap);
                DropPodUtility.DropThingsNear(dropSpot, targetMap, deliveredThings, 110, canRoofPunch: false, forbid: false);
                Messages.Message($"[MARKETPLACE] Express Drop-Pod orbital delivery launched! {lot.StackCount}x {lot.ItemLabel} arriving near trade beacon.", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                IntVec3 depositCell = FindBestDepositCell(targetMap, thingDef);
                foreach (var th in deliveredThings)
                {
                    GenPlace.TryPlaceThing(th, depositCell, targetMap, ThingPlaceMode.Near);
                }
                Messages.Message($"[MARKETPLACE] Warehouse logistics delivery complete: {lot.StackCount}x {lot.ItemLabel} transferred to colony storage.", MessageTypeDefOf.PositiveEvent);
            }

            return true;
        }

        public bool RentMercenaryPlatoon(MercenaryPlatoonContract contract, Map targetMap)
        {
            if (contract == null) return false;
            targetMap = targetMap ?? Find.CurrentMap;
            if (targetMap == null) return false;

            if (NationalTreasurySilver < contract.DailyFeeSilver)
            {
                Messages.Message($"[PMC] Insufficient treasury silver to recruit {contract.PlatoonName}. Daily fee: {contract.DailyFeeSilver:F0} Silver.", MessageTypeDefOf.RejectInput);
                return false;
            }

            NationalTreasurySilver -= contract.DailyFeeSilver;
            contract.IsHired = true;
            contract.RenterLogin = SessionClientController.My?.Login ?? "Local";

            Faction friendlyFaction = Find.FactionManager.AllFactions.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.IsPlayer && !f.def.hidden) ?? Faction.OfEmpire ?? Faction.OfPlayer;
            IntVec3 spawnSpot = DropCellFinder.TradeDropSpot(targetMap);

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner") ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Colonist") ?? PawnKindDefOf.Colonist;

            for (int i = 0; i < contract.SoldierCount; i++)
            {
                Pawn soldier = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind,
                    friendlyFaction,
                    PawnGenerationContext.NonPlayer,
                    -1,
                    forceGenerateNewPawn: true
                ));
                GenSpawn.Spawn(soldier, CellFinder.RandomClosewalkCellNear(spawnSpot, targetMap, 6), targetMap);
            }

            Messages.Message($"[PMC CONTRACT] {contract.PlatoonName} ({contract.SoldierCount} soldiers) deployed as allied defenders on {targetMap.info?.parent?.LabelCap ?? "settlement"}!", MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool AcceptLoan(SovereignLoanContract loan, int collateralTile, string collateralName)
        {
            if (loan == null || loan.IsActive) return false;

            loan.IsActive = true;
            loan.BorrowerLogin = SessionClientController.My?.Login ?? "Local";
            loan.CollateralSettlementTile = collateralTile;
            loan.CollateralSettlementName = collateralName;
            loan.RepaymentDueTick = Find.TickManager.TicksGame + (60000 * 15);

            NationalTreasurySilver += loan.PrincipalSilver;

            Messages.Message($"[SOVEREIGN LOAN] Loan contract accepted! {loan.PrincipalSilver:F0} Silver deposited into State Treasury. Pledged collateral: {collateralName} (Tile #{collateralTile}). Repayment of {loan.TotalRepaymentDue:F0} Silver due in 15 days.", MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool RepayLoan(SovereignLoanContract loan)
        {
            if (loan == null || !loan.IsActive) return false;

            if (NationalTreasurySilver < loan.TotalRepaymentDue)
            {
                Messages.Message($"[SOVEREIGN LOAN] Insufficient funds to repay loan! Total due: {loan.TotalRepaymentDue:F0} Silver (Treasury: {NationalTreasurySilver:F0}).", MessageTypeDefOf.RejectInput);
                return false;
            }

            NationalTreasurySilver -= loan.TotalRepaymentDue;
            loan.IsActive = false;
            loan.IsRepaid = true;

            Messages.Message($"[SOVEREIGN LOAN] Loan successfully repaid with interest ({loan.TotalRepaymentDue:F0} Silver). Collateral on {loan.CollateralSettlementName} released!", MessageTypeDefOf.PositiveEvent);
            return true;
        }



        public SettlementStabilityTracker GetOrCreateStabilityTracker(int tile, string name)
        {
            if (!stabilityTrackers.TryGetValue(tile, out var tracker))
            {
                tracker = new SettlementStabilityTracker(tile, name);
                stabilityTrackers[tile] = tracker;
            }
            return tracker;
        }

        public ProvincialArchetypeDef GetArchetype(int tile)
        {
            if (settlementArchetypes.TryGetValue(tile, out var defName))
            {
                return DefDatabase<ProvincialArchetypeDef>.GetNamedSilentFail(defName);
            }
            return null;
        }

        public void SetArchetype(int tile, ProvincialArchetypeDef archetype)
        {
            if (archetype == null)
            {
                settlementArchetypes.Remove(tile);
            }
            else
            {
                settlementArchetypes[tile] = archetype.defName;
            }
        }

        public int CapitalTile = -1;
        private Map lastFocusMap = null;

        public void SetGovernor(int tile, string pawnThingId)
        {
            if (string.IsNullOrEmpty(pawnThingId))
            {
                settlementGovernors.Remove(tile);
            }
            else
            {
                settlementGovernors[tile] = pawnThingId;
            }
        }

        public string GetGovernorId(int tile)
        {
            settlementGovernors.TryGetValue(tile, out string id);
            return id;
        }

        public void SetCapital(int tile)
        {
            CapitalTile = tile;
        }

        public bool IsTileAdjacentToBorder(int tile, int maxDistance = 2)
        {
            if (RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance == null) return true;
            var claimed = RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance.ClaimedTiles;
            if (claimed == null || claimed.Count == 0) return true; // First settlement
            if (claimed.Contains(tile)) return true;

            var neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors((PlanetTile)tile, neighbors);
            foreach (var pt in neighbors)
            {
                if (claimed.Contains(pt.tileId)) return true;
            }
            if (maxDistance > 1)
            {
                foreach (var pt in neighbors)
                {
                    var subNeighbors = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(pt, subNeighbors);
                    foreach (var spt in subNeighbors)
                    {
                        if (claimed.Contains(spt.tileId)) return true;
                    }
                }
            }
            return false;
        }

        public Dictionary<int, string> GetAllArchetypes() => settlementArchetypes;
        public Dictionary<int, string> GetAllGovernors() => settlementGovernors;
        public Dictionary<int, SettlementStabilityTracker> GetAllTrackers() => stabilityTrackers;

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // Zero-lag focus handover: immediately deposit buffered yields when player focuses a map
            if (Find.CurrentMap != null && Find.CurrentMap != lastFocusMap)
            {
                lastFocusMap = Find.CurrentMap;
                DepositPendingYieldsToMap(lastFocusMap);
            }

            macroTickCounter++;
            if (macroTickCounter >= MacroTickInterval)
            {
                macroTickCounter = 0;
                DoMacroSimulationTick(MacroTickInterval);
            }
        }

        private void DoMacroSimulationTick(int ticksPassed)
        {
            if (Current.Game == null || Find.Maps == null) return;

            float deltaDays = (float)ticksPassed / 60000f;
            Map focusMap = Find.CurrentMap;

            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;

                // FocusMap runs with 100% vanilla fidelity. Only background vassal maps run macro-sim!
                if (map == focusMap) continue;

                ExecuteBackgroundMapMacroSim(map, deltaDays);
            }
        }

        private void ExecuteBackgroundMapMacroSim(Map map, float deltaDays)
        {
            int tile = map.Tile;
            string colonyName = map.info != null && map.info.parent != null ? map.info.parent.LabelCap : "Колония";
            var stability = GetOrCreateStabilityTracker(tile, colonyName);

            var colonists = map.mapPawns.FreeColonists;
            int pawnCount = colonists.Count;

            Pawn governor = null;
            if (settlementGovernors.TryGetValue(tile, out string govId))
            {
                governor = colonists.FirstOrDefault(p => p.ThingID == govId);
            }

            var archetype = GetArchetype(tile);

            // Update stability factors with governor personality & archetype modifiers
            stability.MacroTickStability(deltaDays, governor, archetype);

            if (pawnCount <= 0) return;

            float avgSkill = 8.0f;
            float yieldAmount = MacroYieldMath.CalculateProduction(
                pawnCount,
                avgSkill,
                archetype,
                governor,
                stability.CurrentStage,
                deltaDays);

            if (yieldAmount > 0f)
            {
                string resDefName = archetype != null ? archetype.PrimaryProductDefName : "RawPotatoes";
                AddPendingYield(tile, resDefName, yieldAmount);
            }
        }

        public void AddPendingYield(int tile, string defName, float amount)
        {
            if (!pendingYields.TryGetValue(tile, out var dict))
            {
                dict = new Dictionary<string, float>();
                pendingYields[tile] = dict;
            }

            if (dict.ContainsKey(defName))
            {
                dict[defName] += amount;
            }
            else
            {
                dict[defName] = amount;
            }
        }

        private IntVec3 FindBestDepositCell(Map map, ThingDef thingDef)
        {
            if (map == null || thingDef == null) return IntVec3.Invalid;

            // 1. Priority: designated Zone_Stockpile accepting this item with available space
            if (map.zoneManager != null && map.zoneManager.AllZones != null)
            {
                var stockpiles = map.zoneManager.AllZones.OfType<Zone_Stockpile>();
                foreach (var stockpile in stockpiles)
                {
                    if (stockpile.settings == null || !stockpile.settings.AllowedToAccept(thingDef))
                        continue;

                    foreach (var cell in stockpile.Cells)
                    {
                        if (!cell.Standable(map)) continue;
                        var existingThing = cell.GetFirstThing(map, thingDef);
                        if (existingThing != null && existingThing.stackCount < thingDef.stackLimit)
                        {
                            return cell;
                        }
                        if (cell.GetFirstItem(map) == null)
                        {
                            return cell;
                        }
                    }
                }
            }

            // 2. Priority: Storage buildings / containers (shelves, racks) accepting this item
            if (map.listerBuildings != null && map.listerBuildings.allBuildingsColonist != null)
            {
                var storageBuildings = map.listerBuildings.allBuildingsColonist
                    .OfType<Building_Storage>();
                foreach (var storage in storageBuildings)
                {
                    if (storage.settings == null || !storage.settings.AllowedToAccept(thingDef))
                        continue;

                    foreach (var cell in storage.AllSlotCells())
                    {
                        if (!cell.Standable(map)) continue;
                        var existingThing = cell.GetFirstThing(map, thingDef);
                        if (existingThing != null && existingThing.stackCount < thingDef.stackLimit)
                        {
                            return cell;
                        }
                        if (cell.GetFirstItem(map) == null)
                        {
                            return cell;
                        }
                    }
                }
            }

            // 3. Fallback: vanilla trade drop spot or raid drop center
            IntVec3 fallback = DropCellFinder.TradeDropSpot(map);
            if (fallback.IsValid && fallback.InBounds(map)) return fallback;
            return map.Center;
        }

        public void DepositPendingYieldsToMap(Map map)
        {
            if (map == null || !pendingYields.TryGetValue(map.Tile, out var dict)) return;

            try
            {
                foreach (var kvp in dict.ToList())
                {
                    int countToSpawn = Mathf.FloorToInt(kvp.Value);
                    if (countToSpawn > 0)
                    {
                        ThingDef thingDef = RimNations.Core.GrandStrategy.Compatibility.ModCompatibilityManager.ResolveSafeResourceDef(kvp.Key);
                        if (thingDef != null)
                        {
                            IntVec3 dropCell = FindBestDepositCell(map, thingDef);
                            int stackLimit = Math.Max(1, thingDef.stackLimit);
                            while (countToSpawn > 0)
                            {
                                int currentBatch = Math.Min(countToSpawn, stackLimit);
                                Thing spawned = ThingMaker.MakeThing(thingDef);
                                spawned.stackCount = currentBatch;
                                GenPlace.TryPlaceThing(spawned, dropCell, map, ThingPlaceMode.Near);
                                countToSpawn -= currentBatch;
                            }
                        }
                        dict[kvp.Key] = 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                RimNations.Core.GrandStrategy.Compatibility.ModCompatibilityManager.HandlePatchException(ex, "DepositPendingYieldsToMap");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CapitalTile, "CapitalTile", -1);
            Scribe_Values.Look(ref NationalCustomsTariff, "NationalCustomsTariff", 0.05f);
            Scribe_Values.Look(ref NationalTreasurySilver, "NationalTreasurySilver", 1000f);
            Scribe_Values.Look(ref BorderBlockadeActive, "BorderBlockadeActive", false);
            Scribe_Values.Look(ref CustomColorSet, "CustomColorSet", false);
            Scribe_Values.Look(ref NationalColor, "NationalColor", new Color(0.18f, 0.55f, 0.95f, 0.45f));
            Scribe_Values.Look(ref GovType, "GovType", GovernmentType.Republic);

            Scribe_Collections.Look(ref stabilityTrackers, "stabilityTrackers", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref settlementArchetypes, "settlementArchetypes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref settlementGovernors, "settlementGovernors", LookMode.Value, LookMode.Value);

            Scribe_Collections.Look(ref MinistryOffices, "MinistryOffices", LookMode.Deep);
            Scribe_Collections.Look(ref Citizens, "Citizens", LookMode.Deep);
            Scribe_Collections.Look(ref MarketLots, "MarketLots", LookMode.Deep);
            Scribe_Collections.Look(ref MercenaryContracts, "MercenaryContracts", LookMode.Deep);
            Scribe_Collections.Look(ref LoanContracts, "LoanContracts", LookMode.Deep);

            if (stabilityTrackers == null) stabilityTrackers = new Dictionary<int, SettlementStabilityTracker>();
            if (settlementArchetypes == null) settlementArchetypes = new Dictionary<int, string>();
            if (settlementGovernors == null) settlementGovernors = new Dictionary<int, string>();
            if (pendingYields == null) pendingYields = new Dictionary<int, Dictionary<string, float>>();
            if (MinistryOffices == null) MinistryOffices = new List<MinistryOffice>();
            if (Citizens == null) Citizens = new List<CitizenRecord>();
            if (MarketLots == null) MarketLots = new List<MarketTradeLot>();
            if (MercenaryContracts == null) MercenaryContracts = new List<MercenaryPlatoonContract>();
            if (LoanContracts == null) LoanContracts = new List<SovereignLoanContract>();
        }
    }
}
