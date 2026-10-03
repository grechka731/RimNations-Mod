using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using OCUnion;
using OCUnion.Transfer.Model;
using RimNations.Core.GrandStrategy.Core;
using RimNations.Core.GrandStrategy.Economics;
using RimNations.Core.GrandStrategy.Stability;
using RimWorld;
using RimWorld.Planet;
using RimWorldOnlineCity;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    public class Dialog_RimNationsDashboard : Window
    {
        public enum DashboardTab
        {
            MyState = 0,
            MapBorders = 1,
            Diplomacy = 2,
            WarDepartment = 3,
            CustomsMarket = 4
        }

        public enum MyStateSubView
        {
            Provinces = 0,
            Ministries = 1,
            Citizens = 2
        }

        public enum DiplomacySubView
        {
            Pacts = 0,
            MercenaryBoard = 1,
            SovereignLoans = 2
        }

        private DashboardTab curTab = DashboardTab.MyState;
        private MyStateSubView curMyStateSubView = MyStateSubView.Provinces;
        private DiplomacySubView curDiplomacySubView = DiplomacySubView.Pacts;
        private MarketCategory selectedMarketCategory = MarketCategory.All;
        private GovernmentType foundingGovType = GovernmentType.Monarchy;

        // State foundation
        private string newNationName = "New Sovereign Realm";

        // Market inputs
        private string tradeItemDefName = "Steel";
        private string tradeCountStr = "100";
        private string tradePriceStr = "2";
        private bool isBuyOrder = false;

        // War Department inputs
        private string selectedDefenderLogin = "";
        private string selectedCasusBelli = "Border Incursion";
        private readonly string[] casusBelliOptions = new string[]
        {
            "Border Incursion",
            "Customs Violation / Trade Embargo",
            "Sovereignty Reclamation",
            "Ideological Crusade"
        };

        // Scroll positions
        private Vector2 scrollPosProvinces = Vector2.zero;
        private Vector2 scrollPosMinistries = Vector2.zero;
        private Vector2 scrollPosCitizens = Vector2.zero;
        private Vector2 scrollPosDiplomacy = Vector2.zero;
        private Vector2 scrollPosMercenaries = Vector2.zero;
        private Vector2 scrollPosLoans = Vector2.zero;
        private Vector2 scrollPosMarket = Vector2.zero;
        public override Vector2 InitialSize => new Vector2(960f, 680f);

        public Dialog_RimNationsDashboard()
        {
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var gov = WorldComponent_StateGovernor.Instance;
            if (gov == null)
            {
                Widgets.Label(inRect, "RimNations State Governor engine is not initialized.");
                return;
            }

            // Top Header
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 36f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "RIMNATIONS GEOPOLITICAL MMO & GRAND STRATEGY ENGINE");
            Text.Font = GameFont.Small;

            // Tab bar
            Rect tabBarRect = new Rect(inRect.x, inRect.y + 42f, inRect.width, 34f);
            DrawTabs(tabBarRect);

            // Tab contents
            Rect contentRect = new Rect(inRect.x, inRect.y + 84f, inRect.width, inRect.height - 130f);
            Widgets.DrawMenuSection(contentRect);
            Rect innerContent = contentRect.ContractedBy(10f);

            switch (curTab)
            {
                case DashboardTab.MyState:
                    DrawTabMyState(innerContent, gov);
                    break;
                case DashboardTab.MapBorders:
                    DrawTabMapBorders(innerContent, gov);
                    break;
                case DashboardTab.Diplomacy:
                    DrawTabDiplomacy(innerContent, gov);
                    break;
                case DashboardTab.WarDepartment:
                    DrawTabWarDepartment(innerContent, gov);
                    break;
                case DashboardTab.CustomsMarket:
                    DrawTabCustomsMarket(innerContent, gov);
                    break;
            }
        }

        private void DrawTabs(Rect rect)
        {
            string[] tabNames = new string[]
            {
                "My State",
                "Map & Borders",
                "Diplomacy",
                "War Department",
                "Customs & Market"
            };

            float tabWidth = (rect.width - 20f) / tabNames.Length;
            for (int i = 0; i < tabNames.Length; i++)
            {
                Rect buttonRect = new Rect(rect.x + (i * (tabWidth + 5f)), rect.y, tabWidth, rect.height);
                bool isActive = ((int)curTab == i);
                if (isActive)
                {
                    GUI.color = Color.yellow;
                }
                if (Widgets.ButtonText(buttonRect, tabNames[i]))
                {
                    curTab = (DashboardTab)i;
                }
                GUI.color = Color.white;
            }
        }

        #region TAB 0: MY STATE
        public static string GetGovPerkDescription(GovernmentType type)
        {
            switch (type)
            {
                case GovernmentType.Monarchy:
                    return "Monarchy: -10% Capital Unrest, +15% Sovereign Authority";
                case GovernmentType.Republic:
                    return "Republic: +20% Provincial Trade Yields, +5 Max Citizenship Capacity";
                case GovernmentType.MilitaryJunta:
                    return "Military Junta: +25% Militia Combat Effectiveness, -15% PMC Rental Costs";
                case GovernmentType.Technocracy:
                    return "Technocracy: +20% Component & Research Output, +10% Production Efficiency";
                case GovernmentType.MerchantGuild:
                    return "Merchant Guild: +30% Customs Tariff Revenue, +15% Treasury Yields";
                default:
                    return "Sovereign State";
            }
        }

        private void DrawTabMyState(Rect rect, WorldComponent_StateGovernor gov)
        {
            string playerName = SessionClientController.My?.Login ?? "Local Sovereign";
            string stateName = SessionClientController.My?.StateName;

            if (string.IsNullOrEmpty(stateName))
            {
                Listing_Standard listing = new Listing_Standard();
                listing.Begin(rect);

                listing.Label("You have not officially established a sovereign state on the world stage.");
                listing.Gap(6f);
                listing.Label("Founding a nation enables territorial claims, customs collection, and diplomatic treaties.");
                listing.Gap(6f);
                newNationName = listing.TextEntry(newNationName);
                listing.Gap(6f);

                listing.Label($"Government Form: {foundingGovType}");
                if (listing.ButtonText($"Select Form of Government ({foundingGovType})"))
                {
                    List<FloatMenuOption> govOptions = new List<FloatMenuOption>();
                    foreach (GovernmentType gt in Enum.GetValues(typeof(GovernmentType)))
                    {
                        var optGt = gt;
                        govOptions.Add(new FloatMenuOption($"{optGt} - {GetGovPerkDescription(optGt)}", () => foundingGovType = optGt));
                    }
                    Find.WindowStack.Add(new FloatMenu(govOptions));
                }
                GUI.color = Color.cyan;
                listing.Label($"Passive Perk: {GetGovPerkDescription(foundingGovType)}");
                GUI.color = Color.white;
                listing.Gap(10f);

                if (listing.ButtonText("Proclaim Sovereign Nation (0 Silver)"))
                {
                    if (!string.IsNullOrEmpty(newNationName))
                    {
                        gov.GovType = foundingGovType;
                        var packet = new PacketCreateNation()
                        {
                            StateName = newNationName.Trim(),
                            CapitalTile = Find.CurrentMap != null ? (int)Find.CurrentMap.Tile : 0
                        };

                        SessionClientController.Command((connect) =>
                        {
                            connect.CreateNation(packet);
                        });

                        if (Find.CurrentMap != null)
                        {
                            gov.SetCapital((int)Find.CurrentMap.Tile);
                        }

                        Messages.Message($"[RIMNATIONS] The sovereign nation of '{newNationName}' ({gov.GovType}) has been proclaimed!", MessageTypeDefOf.PositiveEvent);
                    }
                }
                listing.End();
                return;
            }

            // State Info Header (Height 62f)
            Rect headerBox = new Rect(rect.x, rect.y, rect.width, 62f);
            Widgets.DrawBoxSolid(headerBox, new Color(0.14f, 0.14f, 0.16f, 0.85f));
            Widgets.DrawBox(headerBox);

            int capTile = gov.CapitalTile >= 0 ? gov.CapitalTile : (Find.CurrentMap != null ? (int)Find.CurrentMap.Tile : 0);
            string h1 = $"Sovereign Nation: {stateName} | Head of State: {playerName} | Capital: Tile #{capTile}";
            string h2 = $"Treasury Reserves: {gov.NationalTreasurySilver:F0} Silver | Form: {gov.GovType} ({GetGovPerkDescription(gov.GovType)})";

            Widgets.Label(new Rect(headerBox.x + 10f, headerBox.y + 6f, headerBox.width - 200f, 24f), h1);
            Widgets.Label(new Rect(headerBox.x + 10f, headerBox.y + 32f, headerBox.width - 200f, 24f), h2);

            // Change Government Form button
            if (Widgets.ButtonText(new Rect(headerBox.xMax - 180f, headerBox.y + 15f, 170f, 32f), "Reform Constitution"))
            {
                if (!gov.HasPermission(playerName, MinistryPermission.InviteBanishCitizens))
                {
                    Messages.Message("[SOVEREIGN LAW] Only the Head of State can reform the national constitution.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    List<FloatMenuOption> govOptions = new List<FloatMenuOption>();
                    foreach (GovernmentType gt in Enum.GetValues(typeof(GovernmentType)))
                    {
                        var optGt = gt;
                        govOptions.Add(new FloatMenuOption($"{optGt} - {GetGovPerkDescription(optGt)}", () =>
                        {
                            gov.GovType = optGt;
                            Messages.Message($"[CONSTITUTION] Reformed government to {optGt}!", MessageTypeDefOf.PositiveEvent);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(govOptions));
                }
            }

            // Sub-Navigation Bar
            Rect subNavRect = new Rect(rect.x, rect.y + 68f, rect.width, 30f);
            string[] subTabs = new string[] { "Provinces & Empire", "Government & Ministries", "Citizenship & Immigration" };
            float subTabWidth = (subNavRect.width - 10f) / subTabs.Length;
            for (int i = 0; i < subTabs.Length; i++)
            {
                Rect bRect = new Rect(subNavRect.x + i * (subTabWidth + 5f), subNavRect.y, subTabWidth, subNavRect.height);
                if ((int)curMyStateSubView == i) GUI.color = Color.yellow;
                if (Widgets.ButtonText(bRect, subTabs[i]))
                {
                    curMyStateSubView = (MyStateSubView)i;
                }
                GUI.color = Color.white;
            }

            Rect subContentRect = new Rect(rect.x, rect.y + 104f, rect.width, rect.height - 108f);

            switch (curMyStateSubView)
            {
                case MyStateSubView.Provinces:
                    DrawSubViewProvinces(subContentRect, gov, playerName);
                    break;
                case MyStateSubView.Ministries:
                    DrawSubViewMinistries(subContentRect, gov, playerName);
                    break;
                case MyStateSubView.Citizens:
                    DrawSubViewCitizens(subContentRect, gov, playerName, stateName);
                    break;
            }
        }

        private void DrawSubViewProvinces(Rect rect, WorldComponent_StateGovernor gov, string playerName)
        {
            var playerMaps = Find.Maps?.Where(m => m != null && m.IsPlayerHome).ToList() ?? new List<Map>();
            Rect viewRect = new Rect(0, 0, rect.width - 25f, Math.Max(120f, playerMaps.Count * 94f));
            Widgets.BeginScrollView(rect, ref scrollPosProvinces, viewRect);

            float curY = 0f;
            foreach (var map in playerMaps)
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 86f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.18f, 0.8f));
                GUI.color = Color.gray;
                Widgets.DrawBox(rowRect);
                GUI.color = Color.white;

                int tile = map.Tile;
                string colonyName = map.info?.parent?.LabelCap ?? $"Province #{tile}";
                var stability = gov.GetOrCreateStabilityTracker(tile, colonyName);
                var archetype = gov.GetArchetype(tile);
                string govId = gov.GetGovernorId(tile);
                Pawn governorPawn = map.mapPawns.FreeColonists.FirstOrDefault(p => p.ThingID == govId);

                // Column 1: Info & Capital (210f)
                Rect col1 = new Rect(rowRect.x + 8f, rowRect.y + 6f, 210f, 74f);
                bool isCapital = (gov.CapitalTile == tile);
                string title = isCapital ? $"[CAPITAL] {colonyName}" : colonyName;
                Widgets.Label(new Rect(col1.x, col1.y, col1.width, 24f), title);

                if (!isCapital && Widgets.ButtonText(new Rect(col1.x, col1.y + 32f, 130f, 28f), "Set as Capital"))
                {
                    gov.SetCapital(tile);
                    Messages.Message($"[RIMNATIONS] {colonyName} is now the sovereign Capital!", MessageTypeDefOf.PositiveEvent);
                }

                // Column 2: Archetype (180f)
                Rect col2 = new Rect(col1.xMax + 10f, rowRect.y + 6f, 180f, 74f);
                string archLabel = archetype != null ? archetype.label : "[Standard Base]";
                Widgets.Label(new Rect(col2.x, col2.y, col2.width, 24f), $"Archetype: {archLabel}");

                if (Widgets.ButtonText(new Rect(col2.x, col2.y + 32f, 160f, 28f), "Change Archetype"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (var a in DefDatabase<ProvincialArchetypeDef>.AllDefs)
                    {
                        var optDef = a;
                        options.Add(new FloatMenuOption($"{optDef.label} (Yield: {optDef.PrimaryProductDefName})", () =>
                        {
                            gov.SetArchetype(tile, optDef);
                            Messages.Message($"[RIMNATIONS] {colonyName} specialized as {optDef.label}.", MessageTypeDefOf.TaskCompletion);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                // Column 3: Governor (180f)
                Rect col3 = new Rect(col2.xMax + 10f, rowRect.y + 6f, 180f, 74f);
                string govLabel = governorPawn != null ? governorPawn.LabelShort : "[No Governor]";
                Widgets.Label(new Rect(col3.x, col3.y, col3.width, 24f), $"Governor: {govLabel}");

                if (Widgets.ButtonText(new Rect(col3.x, col3.y + 32f, 160f, 28f), "Assign Governor"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("[Dismiss Governor]", () =>
                    {
                        gov.SetGovernor(tile, null);
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
                        options.Add(new FloatMenuOption($"{colonist.LabelShort} (Soc {soc}){traitNotice}", () =>
                        {
                            gov.SetGovernor(tile, colonist.ThingID);
                            Messages.Message($"[RIMNATIONS] {colonist.LabelShort} appointed Governor of {colonyName}.", MessageTypeDefOf.TaskCompletion);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                // Column 4: Unrest & Stability Stage (140f)
                Rect col4 = new Rect(col3.xMax + 10f, rowRect.y + 6f, 140f, 74f);
                Color stageColor = (stability.CurrentStage == RevoltStage.Peaceful) ? Color.green :
                                   (stability.CurrentStage == RevoltStage.GeneralStrike) ? Color.yellow : Color.red;

                GUI.color = stageColor;
                Widgets.Label(new Rect(col4.x, col4.y, col4.width, 20f), $"Status: {stability.CurrentStage}");
                GUI.color = Color.white;

                Widgets.FillableBar(new Rect(col4.x, col4.y + 24f, 130f, 14f), stability.Unrest / 100f, SolidColorMaterials.NewSolidColorTexture(stageColor));
                Widgets.Label(new Rect(col4.x, col4.y + 40f, col4.width, 20f), $"Unrest: {stability.Unrest:F1}%");

                // Column 5: Imperial Inspector Action (150f)
                Rect col5 = new Rect(col4.xMax + 10f, rowRect.y + 14f, 150f, 34f);
                if (Widgets.ButtonText(col5, "Imperial Inspector (100$)"))
                {
                    gov.DispatchImperialInspector(tile);
                }

                curY += 92f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSubViewMinistries(Rect rect, WorldComponent_StateGovernor gov, string playerName)
        {
            // Top Action Bar
            Rect topBar = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.Label(new Rect(topBar.x, topBar.y + 4f, topBar.width - 230f, 24f), "CONSTITUTIONAL MINISTRIES & EXECUTIVE APPOINTMENTS");

            if (Widgets.ButtonText(new Rect(topBar.xMax - 220f, topBar.y, 220f, 30f), "+ Form Ministry / Office"))
            {
                if (!gov.HasPermission(playerName, MinistryPermission.InviteBanishCitizens))
                {
                    Messages.Message("[SOVEREIGN LAW] Only the Head of State or Minister of State may form new ministries.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_EditPosition(new MinistryOffice(), isNew: true));
                }
            }

            // Ministries List
            Rect listRect = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 40f);
            var offices = gov.MinistryOffices;
            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(120f, offices.Count * 68f));
            Widgets.BeginScrollView(listRect, ref scrollPosMinistries, viewRect);

            float curY = 0f;
            foreach (var off in offices.ToList())
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 60f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f, 0.85f));
                Widgets.DrawBox(rowRect);

                // Col 1: Title & Assignee
                string assignee = string.IsNullOrEmpty(off.AssignedPlayerLogin) ? "[Vacant]" : off.AssignedPlayerLogin;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 6f, 260f, 24f), $"{off.Title}");
                GUI.color = Color.cyan;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 30f, 260f, 24f), $"Assigned: {assignee}");
                GUI.color = Color.white;

                // Col 2: Permission Badges
                Rect badgeRect = new Rect(rowRect.x + 280f, rowRect.y + 16f, 380f, 26f);
                DrawPermBadge(new Rect(badgeRect.x, badgeRect.y, 65f, 24f), "War", off.CanDeclareWar);
                DrawPermBadge(new Rect(badgeRect.x + 72f, badgeRect.y, 65f, 24f), "Peace", off.CanAuthorizePeace);
                DrawPermBadge(new Rect(badgeRect.x + 144f, badgeRect.y, 75f, 24f), "Citizens", off.CanInviteBanishCitizens);
                DrawPermBadge(new Rect(badgeRect.x + 224f, badgeRect.y, 70f, 24f), "Tariffs", off.CanAdjustCustomsTariff);
                DrawPermBadge(new Rect(badgeRect.x + 299f, badgeRect.y, 75f, 24f), "Treasury", off.CanAccessNationalTreasury);

                // Col 3: Actions
                if (Widgets.ButtonText(new Rect(rowRect.xMax - 180f, rowRect.y + 14f, 85f, 30f), "Edit"))
                {
                    Find.WindowStack.Add(new Dialog_EditPosition(off, isNew: false));
                }

                if (Widgets.ButtonText(new Rect(rowRect.xMax - 90f, rowRect.y + 14f, 80f, 30f), "Dissolve"))
                {
                    gov.MinistryOffices.Remove(off);
                    Messages.Message($"[GOVERNMENT] Office '{off.Title}' has been dissolved.", MessageTypeDefOf.CautionInput);
                }

                curY += 66f;
            }

            Widgets.EndScrollView();
        }

        private void DrawPermBadge(Rect rect, string label, bool enabled)
        {
            Color oldCol = GUI.color;
            GUI.color = enabled ? new Color(0.2f, 0.8f, 0.3f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.2f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = enabled ? Color.green : Color.gray;
            Widgets.DrawBox(rect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = oldCol;
        }

        private void DrawSubViewCitizens(Rect rect, WorldComponent_StateGovernor gov, string playerName, string stateName)
        {
            // Top Action Bar
            Rect topBar = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.Label(new Rect(topBar.x, topBar.y + 4f, topBar.width - 290f, 24f), "CITIZENSHIP BOARD & IMMIGRATION ROSTER");

            if (Widgets.ButtonText(new Rect(topBar.xMax - 280f, topBar.y, 280f, 30f), "+ Send Credentials (Invite)"))
            {
                if (!gov.HasPermission(playerName, MinistryPermission.InviteBanishCitizens))
                {
                    Messages.Message("[SOVEREIGN LAW] Only authorized ministers may grant citizenship credentials.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    var serverPlayers = SessionClientController.Data?.Players?.Values.ToList() ?? new List<PlayerClient>();
                    foreach (var p in serverPlayers)
                    {
                        if (gov.Citizens.Any(c => c.PlayerLogin.Equals(p.Public.Login, StringComparison.OrdinalIgnoreCase))) continue;
                        var targetP = p;
                        options.Add(new FloatMenuOption($"Invite {targetP.Public.Login} (Nation: {targetP.Public.StateName ?? "None"})", () =>
                        {
                            gov.Citizens.Add(new CitizenRecord(targetP.Public.Login, "Vassal Citizen", 1));
                            Messages.Message($"[CITIZENSHIP] Diplomatic credentials issued to {targetP.Public.Login}! Added to sovereign citizen roster.", MessageTypeDefOf.PositiveEvent);
                        }));
                    }
                    if (options.Count == 0)
                    {
                        options.Add(new FloatMenuOption("[No unaligned players found on server]", null));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            // Citizens List
            Rect listRect = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 40f);
            var citizens = gov.Citizens;
            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(120f, citizens.Count * 60f));
            Widgets.BeginScrollView(listRect, ref scrollPosCitizens, viewRect);

            float curY = 0f;
            foreach (var c in citizens.ToList())
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 52f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f, 0.85f));
                Widgets.DrawBox(rowRect);

                // Online indicator & Login
                var pClient = SessionClientController.Data?.Players?.Values.FirstOrDefault(p => p.Public.Login.Equals(c.PlayerLogin, StringComparison.OrdinalIgnoreCase));
                bool isOnline = pClient != null && pClient.Online;
                Color statusColor = isOnline ? Color.green : Color.gray;

                GUI.color = statusColor;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 6f, 220f, 22f), $"● {(isOnline ? "ONLINE" : "OFFLINE")} - {c.PlayerLogin}");
                GUI.color = Color.white;

                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 28f, 220f, 22f), $"Joined: {c.JoinDateStr}");

                // Office & Settlements
                Widgets.Label(new Rect(rowRect.x + 250f, rowRect.y + 14f, 240f, 24f), $"Office: {c.AssignedOfficeTitle}");
                Widgets.Label(new Rect(rowRect.x + 500f, rowRect.y + 14f, 180f, 24f), $"Settlements: {c.OwnedSettlementsCount}");

                // Banish / Revoke Button
                bool isLeader = c.PlayerLogin.Equals(playerName, StringComparison.OrdinalIgnoreCase);
                if (!isLeader)
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 180f, rowRect.y + 10f, 170f, 30f), "Revoke (Banish)"))
                    {
                        if (!gov.HasPermission(playerName, MinistryPermission.InviteBanishCitizens))
                        {
                            Messages.Message("[SOVEREIGN LAW] Access denied: Your office lacks permission to banish citizens.", MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            Find.WindowStack.Add(new Dialog_MessageBox(
                                $"Are you certain you wish to revoke citizenship and banish {c.PlayerLogin} from {stateName}?",
                                "Banish Citizen",
                                () =>
                                {
                                    gov.Citizens.Remove(c);
                                    Messages.Message($"[CITIZENSHIP] {c.PlayerLogin} has been banished from {stateName}.", MessageTypeDefOf.CautionInput);
                                },
                                "Cancel",
                                null,
                                "Revoke Citizenship & Banish"
                            ));
                        }
                    }
                }
                else
                {
                    GUI.color = Color.yellow;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(rowRect.xMax - 180f, rowRect.y + 10f, 170f, 30f), "[Sovereign Leader]");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }

                curY += 58f;
            }

            Widgets.EndScrollView();
        }
        #endregion

        #region TAB 1: MAP & BORDERS
        private void DrawTabMapBorders(Rect rect, WorldComponent_StateGovernor gov)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("TERRITORIAL SOVEREIGNTY & BORDER DOCTRINE");
            listing.GapLine();

            listing.Label("1. Sovereign Claim Projection Radii:");
            listing.Label("   - National Capital: Projects a massive sovereign domain of 10 to 15 hexes.");
            listing.Label("   - Provincial Cities: Organic territorial projection of 7 to 10 hexes around settlements.");
            listing.Gap(4f);

            listing.Label("2. Contiguous Expansion Doctrine:");
            listing.Label("   - Founding new settlements is strictly restricted to tiles adjacent to existing borders.");
            listing.Label("   - Disconnected enclaves are blocked to maintain geopolitical integrity.");
            listing.Gap(4f);

            listing.Label("3. Border Customs & Transit Tariffs:");
            listing.Label($"   - Foreign caravans passing through national borders pay tariffs (Current: {gov.NationalCustomsTariff * 100f:F0}%).");
            listing.Label($"   - National Treasury Reserves: {gov.NationalTreasurySilver:F0} Silver.");
            listing.GapLine();

            // Border Blockade Toggle
            bool blockade = gov.BorderBlockadeActive;
            listing.CheckboxLabeled("Enforce Strict Border Blockade (Interdict foreign caravans & contraband)", ref blockade);
            if (blockade != gov.BorderBlockadeActive)
            {
                gov.BorderBlockadeActive = blockade;
                if (SessionClientController.My != null && !string.IsNullOrEmpty(SessionClientController.My.StateName))
                {
                    if (SessionClientController.Data != null && SessionClientController.Data.States != null &&
                        SessionClientController.Data.States.TryGetValue(SessionClientController.My.StateName, out var state))
                    {
                        state.BorderBlockadeActive = blockade;
                    }
                }
                SessionClientController.Command((connect) =>
                {
                    connect.UpdateTariff(new PacketUpdateTariff()
                    {
                        CustomsTariff = gov.NationalCustomsTariff,
                        BorderBlockadeActive = blockade
                    });
                });
                Messages.Message(blockade ? "[RIMNATIONS] Sovereign borders are now CLOSED." : "[RIMNATIONS] Sovereign borders are now OPEN.", MessageTypeDefOf.CautionInput);
            }

            listing.Gap(6f);
            listing.Label("CUSTOMIZE NATIONAL BORDER COLOR (Цвет государственных границ на глобусе):");
            listing.End();

            string playerName = SessionClientController.My?.Login ?? "Local";
            Color activeColor = gov.GetEffectiveColor();

            // Sovereign Heraldry & Color Card
            Rect heraldryRect = new Rect(rect.x, rect.y + 245f, rect.width, 130f);
            Widgets.DrawMenuSection(heraldryRect);

            Rect colorBadge = new Rect(heraldryRect.x + 15f, heraldryRect.y + 15f, 100f, 100f);
            Widgets.DrawBoxSolid(colorBadge, new Color(activeColor.r, activeColor.g, activeColor.b, 0.90f));
            Widgets.DrawBox(colorBadge, 2);

            Rect infoRect = new Rect(colorBadge.xMax + 20f, heraldryRect.y + 15f, heraldryRect.width - 150f, 100f);
            Listing_Standard heraldryListing = new Listing_Standard();
            heraldryListing.Begin(infoRect);
            Text.Font = GameFont.Medium;
            heraldryListing.Label($"Суверенные геральдические цвета: [{playerName}]");
            Text.Font = GameFont.Small;
            heraldryListing.Label($"HEX: #{ColorUtility.ToHtmlStringRGB(activeColor)} | Высококонтрастный суверенный спектр на глобусе");
            heraldryListing.Label("Гексагональные границы: Столица захватывает 10-13 клеток, города — 7-10 клеток.");
            heraldryListing.Label("Защита суверенитета: Наложение на чужие границы запрещено международным правом.");
            heraldryListing.End();

            // Regenerate Button
            Rect regenRect = new Rect(rect.x, heraldryRect.yMax + 15f, 320f, 34f);
            if (Widgets.ButtonText(regenRect, "Обновить 3D-границы на глобусе"))
            {
                RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance?.SetDirty();
                RimNations.Core.GrandStrategy.UI.WorldComponent_NationBorders.Instance?.RegenerateMesh();
                Messages.Message("[RIMNATIONS] 3D-полигоны границ на глобусе перестроены.", MessageTypeDefOf.PositiveEvent);
            }
        }
        #endregion

        #region TAB 2: DIPLOMACY
        private void DrawTabDiplomacy(Rect rect, WorldComponent_StateGovernor gov)
        {
            string playerName = SessionClientController.My?.Login ?? "Local";

            // Sub-Navigation Bar
            Rect subNavRect = new Rect(rect.x, rect.y, rect.width, 30f);
            string[] subTabs = new string[] { "Multilateral Pacts", "Mercenary Board (PMC)", "Sovereign Loans" };
            float subTabWidth = (subNavRect.width - 10f) / subTabs.Length;
            for (int i = 0; i < subTabs.Length; i++)
            {
                Rect bRect = new Rect(subNavRect.x + i * (subTabWidth + 5f), subNavRect.y, subTabWidth, subNavRect.height);
                if ((int)curDiplomacySubView == i) GUI.color = Color.yellow;
                if (Widgets.ButtonText(bRect, subTabs[i]))
                {
                    curDiplomacySubView = (DiplomacySubView)i;
                }
                GUI.color = Color.white;
            }

            Rect contentRect = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 38f);

            switch (curDiplomacySubView)
            {
                case DiplomacySubView.Pacts:
                    DrawSubViewPacts(contentRect, gov, playerName);
                    break;
                case DiplomacySubView.MercenaryBoard:
                    DrawSubViewMercenaryBoard(contentRect, gov, playerName);
                    break;
                case DiplomacySubView.SovereignLoans:
                    DrawSubViewSovereignLoans(contentRect, gov, playerName);
                    break;
            }
        }

        private void DrawSubViewPacts(Rect rect, WorldComponent_StateGovernor gov, string playerName)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 50f));
            listing.Label("INTERNATIONAL DIPLOMACY & MULTILATERAL PACTS");
            listing.Label("Alliances form mutual defense blocs (Article 5: An attack upon one is an attack upon all).");
            listing.GapLine();
            listing.End();

            Rect listRect = new Rect(rect.x, rect.y + 55f, rect.width, rect.height - 60f);
            var players = SessionClientController.Data?.Players?.Values.ToList() ?? new List<PlayerClient>();

            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(200f, players.Count * 65f));
            Widgets.BeginScrollView(listRect, ref scrollPosDiplomacy, viewRect);

            float curY = 0f;
            foreach (var p in players)
            {
                if (SessionClientController.My != null && p.Public.Login == SessionClientController.My.Login) continue;

                Rect rowRect = new Rect(0, curY, viewRect.width, 58f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.18f, 0.7f));
                Widgets.DrawBox(rowRect);

                // Online indicator
                bool isOnline = p.Online;
                Color statusColor = isOnline ? Color.green : Color.gray;
                string statusText = isOnline ? "ONLINE" : "OFFLINE";

                GUI.color = statusColor;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 8f, 100f, 24f), $"● {statusText}");
                GUI.color = Color.white;

                string stateStr = !string.IsNullOrEmpty(p.Public.StateName) ? $"Nation: {p.Public.StateName}" : "[No State]";
                Widgets.Label(new Rect(rowRect.x + 120f, rowRect.y + 8f, 300f, 24f), $"{p.Public.Login} | {stateStr}");
                Widgets.Label(new Rect(rowRect.x + 120f, rowRect.y + 30f, 300f, 24f), $"Total Wealth: {p.CostWorldObjects().MarketValue:F0} Silver");

                // Treaty / Pact status
                if (Widgets.ButtonText(new Rect(rowRect.xMax - 220f, rowRect.y + 12f, 200f, 32f), "Propose Non-Aggression"))
                {
                    if (!gov.HasPermission(playerName, MinistryPermission.AuthorizePeace))
                    {
                        Messages.Message("[SOVEREIGN LAW] Access denied: Your office lacks permission to sign international treaties.", MessageTypeDefOf.RejectInput);
                    }
                    else
                    {
                        Messages.Message($"[RIMNATIONS] Non-aggression diplomatic dispatch transmitted to {p.Public.Login}.", MessageTypeDefOf.TaskCompletion);
                    }
                }

                curY += 64f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSubViewMercenaryBoard(Rect rect, WorldComponent_StateGovernor gov, string playerName)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 50f));
            listing.Label("PRIVATE MILITARY CONTRACTING (PMC / TROOP LEASING)");
            listing.Label("Contract standing combat squads to defend sovereign settlements from siege or insurrection.");
            listing.GapLine();
            listing.End();

            Rect listRect = new Rect(rect.x, rect.y + 55f, rect.width, rect.height - 60f);
            var contracts = gov.MercenaryContracts;
            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(120f, contracts.Count * 74f));
            Widgets.BeginScrollView(listRect, ref scrollPosMercenaries, viewRect);

            float curY = 0f;
            foreach (var contract in contracts.ToList())
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 66f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f, 0.85f));
                Widgets.DrawBox(rowRect);

                // Col 1: Platoon Info
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 8f, 320f, 24f), $"{contract.PlatoonName}");
                GUI.color = Color.cyan;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 32f, 320f, 24f), $"Provider: {contract.ProviderLogin} ({contract.ProviderState})");
                GUI.color = Color.white;

                // Col 2: Strength & Rate
                Widgets.Label(new Rect(rowRect.x + 340f, rowRect.y + 8f, 240f, 24f), $"Strength: {contract.SoldierCount} Heavy Infantry");
                Widgets.Label(new Rect(rowRect.x + 340f, rowRect.y + 32f, 240f, 24f), $"Retainer: {contract.DailyFeeSilver:F0} Silver/day ({contract.DurationDays}d term)");

                // Col 3: Action
                if (contract.IsHired)
                {
                    GUI.color = Color.yellow;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(rowRect.xMax - 220f, rowRect.y + 16f, 210f, 32f), $"[ACTIVE: {contract.RenterLogin}]");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 220f, rowRect.y + 16f, 210f, 32f), $"Rent Platoon ({contract.DailyFeeSilver:F0}$)"))
                    {
                        if (!gov.HasPermission(playerName, MinistryPermission.DeclareWar))
                        {
                            Messages.Message("[SOVEREIGN LAW] Access denied: Military contracting requires Ministry of War authority.", MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            gov.RentMercenaryPlatoon(contract, Find.CurrentMap);
                        }
                    }
                }

                curY += 72f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSubViewSovereignLoans(Rect rect, WorldComponent_StateGovernor gov, string playerName)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 50f));
            listing.Label("SOVEREIGN COLLATERALIZED LOAN FACILITY");
            listing.Label("Borrow development silver against border colonies. Default triggers peaceful legal annexation by the lender.");
            listing.GapLine();
            listing.End();

            Rect listRect = new Rect(rect.x, rect.y + 55f, rect.width, rect.height - 60f);
            var loans = gov.LoanContracts;
            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(120f, loans.Count * 80f));
            Widgets.BeginScrollView(listRect, ref scrollPosLoans, viewRect);

            float curY = 0f;
            foreach (var loan in loans.ToList())
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 74f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f, 0.85f));
                Widgets.DrawBox(rowRect);

                // Col 1: Lender & Principal
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 8f, 320f, 24f), $"{loan.LenderLogin}");
                GUI.color = Color.green;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 32f, 320f, 24f), $"Principal: +{loan.PrincipalSilver:F0} Silver (Interest: {loan.InterestRate * 100f:F0}%)");
                GUI.color = Color.white;

                // Col 2: Repayment & Collateral
                Widgets.Label(new Rect(rowRect.x + 340f, rowRect.y + 8f, 280f, 24f), $"Total Due: {loan.TotalRepaymentDue:F0} Silver");
                string colNotice = loan.CollateralSettlementTile >= 0 ? $"Pledged: {loan.CollateralSettlementName} (#{loan.CollateralSettlementTile})" : "Collateral: Border Settlement";
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(rowRect.x + 340f, rowRect.y + 32f, 280f, 24f), colNotice);
                GUI.color = Color.white;

                // Col 3: Actions
                if (loan.IsRepaid)
                {
                    GUI.color = Color.green;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(rowRect.xMax - 220f, rowRect.y + 20f, 210f, 32f), "[REPAID & RELEASED]");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
                else if (loan.IsActive)
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 220f, rowRect.y + 20f, 210f, 32f), $"Repay ({loan.TotalRepaymentDue:F0} Silver)"))
                    {
                        if (!gov.HasPermission(playerName, MinistryPermission.AccessNationalTreasury))
                        {
                            Messages.Message("[SOVEREIGN LAW] Access denied: Loan repayment requires Treasury authorization.", MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            gov.RepayLoan(loan);
                        }
                    }
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 220f, rowRect.y + 20f, 210f, 32f), "Accept & Pledge Settlement"))
                    {
                        if (!gov.HasPermission(playerName, MinistryPermission.AccessNationalTreasury))
                        {
                            Messages.Message("[SOVEREIGN LAW] Access denied: Accepting loans requires Treasury authorization.", MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            var playerMaps = Find.Maps?.Where(m => m != null && m.IsPlayerHome).ToList() ?? new List<Map>();
                            foreach (var m in playerMaps)
                            {
                                var selMap = m;
                                string mName = selMap.info?.parent?.LabelCap ?? $"Province #{selMap.Tile}";
                                options.Add(new FloatMenuOption($"Pledge {mName} (Tile #{selMap.Tile})", () =>
                                {
                                    gov.AcceptLoan(loan, selMap.Tile, mName);
                                }));
                            }
                            if (options.Count == 0)
                            {
                                options.Add(new FloatMenuOption("[No eligible settlements to pledge]", null));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                    }
                }

                curY += 80f;
            }

            Widgets.EndScrollView();
        }
        #endregion

        #region TAB 3: WAR DEPARTMENT
        private void DrawTabWarDepartment(Rect rect, WorldComponent_StateGovernor gov)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("WAR DEPARTMENT & CIVILIZED WARFARE DOCTRINE");
            listing.Label("Multiplayer warfare requires valid Casus Belli and hardware-enforced online verification.");
            listing.GapLine();

            var targetablePlayers = SessionClientController.Data?.Players?.Values
                .Where(p => SessionClientController.My == null || p.Public.Login != SessionClientController.My.Login)
                .ToList() ?? new List<PlayerClient>();

            listing.Label($"Target Defender: {(string.IsNullOrEmpty(selectedDefenderLogin) ? "[None Selected]" : selectedDefenderLogin)}");

            if (listing.ButtonText("Select Target Player / Nation"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var p in targetablePlayers)
                {
                    var playerOpt = p;
                    bool isOnline = playerOpt.Online;
                    string statusTag = isOnline ? "[ONLINE]" : "[OFFLINE]";
                    string pState = !string.IsNullOrEmpty(playerOpt.Public.StateName) ? playerOpt.Public.StateName : "No Nation";
                    options.Add(new FloatMenuOption($"{playerOpt.Public.Login} {statusTag} - {pState}", () =>
                    {
                        selectedDefenderLogin = playerOpt.Public.Login;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);
            listing.Label($"Casus Belli: {selectedCasusBelli}");
            if (listing.ButtonText("Select Justification for War"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var cb in casusBelliOptions)
                {
                    string cbName = cb;
                    options.Add(new FloatMenuOption(cbName, () =>
                    {
                        selectedCasusBelli = cbName;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(8f);

            // Hardware Online Verification
            PlayerClient defender = targetablePlayers.FirstOrDefault(p => p.Public.Login == selectedDefenderLogin);
            bool defenderIsOnline = defender != null && defender.Online;

            if (!string.IsNullOrEmpty(selectedDefenderLogin) && !defenderIsOnline)
            {
                GUI.color = Color.red;
                listing.Label("DEFENDER IS OFFLINE! Civilized warfare treaty prohibits surprise attacks on offline players.");
                GUI.color = Color.white;
            }

            // Declare War Button
            bool canDeclare = !string.IsNullOrEmpty(selectedDefenderLogin) && defenderIsOnline;
            if (!canDeclare) GUI.color = Color.gray;

            if (listing.ButtonText("Issue Formal Declaration of War (15m Preparation)") && canDeclare)
            {
                string myLogin = SessionClientController.My?.Login ?? "Local";
                if (!gov.HasPermission(myLogin, MinistryPermission.DeclareWar))
                {
                    Messages.Message("[SOVEREIGN LAW] Access denied: Your assigned office lacks constitutional authority to declare war.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    var packet = new PacketDeclareWar()
                    {
                        DefenderLogin = selectedDefenderLogin,
                        CasusBelli = selectedCasusBelli
                    };

                    SessionClientController.Command((connect) =>
                    {
                        connect.DeclareWar(packet);
                    });
                    Messages.Message($"[RIMNATIONS] Formal Declaration of War transmitted against {selectedDefenderLogin}! 15-minute countdown begun.", MessageTypeDefOf.ThreatBig);
                }
            }
            GUI.color = Color.white;

            listing.GapLine();

            // Militia mobilization
            listing.Label("DEFENSIVE MOBILIZATION:");
            listing.Label("Mobilize armed local militia in your current settlement to defend sovereign territory.");
            if (listing.ButtonText("Mobilize Provincial Militia Battalion"))
            {
                if (Find.CurrentMap != null)
                {
                    var packet = new PacketMobilizeMilitia()
                    {
                        StateName = SessionClientController.My?.StateName,
                        FromTile = Find.CurrentMap.Tile,
                        TargetTile = Find.CurrentMap.Tile,
                        SquadSize = 6,
                        ArchetypeDefName = gov.GetArchetype(Find.CurrentMap.Tile)?.defName
                    };
                    SessionClientController.Command((connect) =>
                    {
                        connect.MobilizeMilitia(packet);
                    });
                    Messages.Message($"[RIMNATIONS] Militia mobilized for defense of {Find.CurrentMap.info?.parent?.LabelCap ?? "settlement"}!", MessageTypeDefOf.PositiveEvent);
                }
            }

            listing.GapLine();
            listing.Label("Civilized Warfare Terms:");
            listing.Label("• Total Annexation: Only allowed if attacker captures >50% of provincial cities before the Capital falls.");
            listing.Label("• Reparations Treaty: Rapid capital capitulation caps reparations at 20-25% daily wealth for 7 days.");
            listing.Label("• AFK Forfeit: Inactivity exceeding 7 consecutive days forfeits contested land to victor.");

            listing.End();
        }
        #endregion

        #region TAB 4: CUSTOMS & MARKET
        private void DrawTabCustomsMarket(Rect rect, WorldComponent_StateGovernor gov)
        {
            string playerName = SessionClientController.My?.Login ?? "Local";

            // 1. Treasury Management Panel (Top ~72f)
            Rect treasuryBox = new Rect(rect.x, rect.y, rect.width, 70f);
            Widgets.DrawBoxSolid(treasuryBox, new Color(0.14f, 0.14f, 0.16f, 0.85f));
            Widgets.DrawBox(treasuryBox);

            Widgets.Label(new Rect(treasuryBox.x + 10f, treasuryBox.y + 8f, 320f, 24f), $"Sovereign Treasury: {gov.NationalTreasurySilver:F0} Silver");

            // Tariff Slider
            Widgets.Label(new Rect(treasuryBox.x + 10f, treasuryBox.y + 36f, 220f, 24f), $"Customs Tariff: {(gov.NationalCustomsTariff * 100f):F1}%");
            float newTariff = Widgets.HorizontalSlider(new Rect(treasuryBox.x + 230f, treasuryBox.y + 40f, 150f, 20f), gov.NationalCustomsTariff, 0.0f, 0.20f);
            if (Math.Abs(newTariff - gov.NationalCustomsTariff) > 0.005f)
            {
                if (!gov.HasPermission(playerName, MinistryPermission.AdjustCustomsTariff))
                {
                    Messages.Message("[SOVEREIGN LAW] Access denied: Your office lacks permission to adjust customs tariffs.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    gov.NationalCustomsTariff = newTariff;
                    SessionClientController.Command((connect) =>
                    {
                        connect.UpdateTariff(new PacketUpdateTariff()
                        {
                            CustomsTariff = newTariff,
                            BorderBlockadeActive = gov.BorderBlockadeActive
                        });
                    });
                }
            }

            // Deposit / Withdraw Buttons
            if (Widgets.ButtonText(new Rect(treasuryBox.xMax - 320f, treasuryBox.y + 18f, 150f, 34f), "Deposit Silver"))
            {
                List<FloatMenuOption> depOptions = new List<FloatMenuOption>();
                depOptions.Add(new FloatMenuOption("Deposit 100 Silver", () => gov.DepositSilverToTreasury(Find.CurrentMap, 100f)));
                depOptions.Add(new FloatMenuOption("Deposit 250 Silver", () => gov.DepositSilverToTreasury(Find.CurrentMap, 250f)));
                depOptions.Add(new FloatMenuOption("Deposit 500 Silver", () => gov.DepositSilverToTreasury(Find.CurrentMap, 500f)));
                depOptions.Add(new FloatMenuOption("Deposit 1000 Silver", () => gov.DepositSilverToTreasury(Find.CurrentMap, 1000f)));
                depOptions.Add(new FloatMenuOption("Deposit All Available", () =>
                {
                    if (Find.CurrentMap != null)
                    {
                        int avail = Find.CurrentMap.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => !t.Position.Fogged(Find.CurrentMap) && t.stackCount > 0).Sum(t => t.stackCount);
                        gov.DepositSilverToTreasury(Find.CurrentMap, avail);
                    }
                }));
                Find.WindowStack.Add(new FloatMenu(depOptions));
            }

            if (Widgets.ButtonText(new Rect(treasuryBox.xMax - 160f, treasuryBox.y + 18f, 150f, 34f), "Withdraw Silver"))
            {
                if (!gov.HasPermission(playerName, MinistryPermission.AccessNationalTreasury))
                {
                    Messages.Message("[SOVEREIGN LAW] Access denied: Withdrawing state silver requires Treasury authorization.", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    List<FloatMenuOption> withOptions = new List<FloatMenuOption>();
                    withOptions.Add(new FloatMenuOption("Withdraw 100 Silver", () => gov.WithdrawSilverFromTreasury(Find.CurrentMap, 100f)));
                    withOptions.Add(new FloatMenuOption("Withdraw 250 Silver", () => gov.WithdrawSilverFromTreasury(Find.CurrentMap, 250f)));
                    withOptions.Add(new FloatMenuOption("Withdraw 500 Silver", () => gov.WithdrawSilverFromTreasury(Find.CurrentMap, 500f)));
                    withOptions.Add(new FloatMenuOption("Withdraw 1000 Silver", () => gov.WithdrawSilverFromTreasury(Find.CurrentMap, 1000f)));
                    Find.WindowStack.Add(new FloatMenu(withOptions));
                }
            }

            // 2. Global Trade Exchange & Order Book
            Rect exchangeHeader = new Rect(rect.x, rect.y + 76f, rect.width, 30f);
            Widgets.Label(new Rect(exchangeHeader.x, exchangeHeader.y + 4f, 320f, 24f), "GLOBAL COMMODITIES EXCHANGE (ORDER BOOK)");

            // Category filter buttons
            Rect filterBar = new Rect(rect.x, rect.y + 108f, rect.width - 210f, 28f);
            MarketCategory[] cats = (MarketCategory[])Enum.GetValues(typeof(MarketCategory));
            float catWidth = (filterBar.width - 10f) / cats.Length;
            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                Rect bRect = new Rect(filterBar.x + i * (catWidth + 2f), filterBar.y, catWidth, filterBar.height);
                if (selectedMarketCategory == c) GUI.color = Color.yellow;
                if (Widgets.ButtonText(bRect, c == MarketCategory.TechArtifacts ? "Tech" : c.ToString()))
                {
                    selectedMarketCategory = c;
                }
                GUI.color = Color.white;
            }

            // Create Listing Button
            if (Widgets.ButtonText(new Rect(rect.xMax - 200f, rect.y + 108f, 200f, 28f), "+ Create Commodity Listing"))
            {
                Find.WindowStack.Add(new Dialog_CreateMarketListing(Find.CurrentMap));
            }

            // 3. Scrollable Order Book Table
            Rect listRect = new Rect(rect.x, rect.y + 142f, rect.width, rect.height - 146f);
            var filteredLots = (selectedMarketCategory == MarketCategory.All)
                ? gov.MarketLots
                : gov.MarketLots.Where(l => l.Category == selectedMarketCategory).ToList();

            Rect viewRect = new Rect(0, 0, listRect.width - 25f, Math.Max(120f, filteredLots.Count * 60f));
            Widgets.BeginScrollView(listRect, ref scrollPosMarket, viewRect);

            float curY = 0f;
            foreach (var lot in filteredLots.ToList())
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 54f);
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f, 0.85f));
                Widgets.DrawBox(rowRect);

                // Col 1: Item Label, Count, Quality, Category
                string qStr = lot.Quality != QualityCategory.Normal ? $" ({lot.Quality})" : "";
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 6f, 280f, 22f), $"{lot.ItemLabel} x{lot.StackCount}{qStr}");
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rowRect.x + 10f, rowRect.y + 28f, 280f, 22f), $"Category: {lot.Category}");
                GUI.color = Color.white;

                // Col 2: Seller
                Widgets.Label(new Rect(rowRect.x + 300f, rowRect.y + 6f, 250f, 22f), $"Seller: {lot.SellerLogin}");
                GUI.color = Color.cyan;
                Widgets.Label(new Rect(rowRect.x + 300f, rowRect.y + 28f, 250f, 22f), $"State: {lot.SellerStateName}");
                GUI.color = Color.white;

                // Col 3: Pricing
                Widgets.Label(new Rect(rowRect.x + 560f, rowRect.y + 6f, 180f, 22f), $"Unit: {lot.UnitPrice:F1} Silver");
                GUI.color = Color.green;
                Widgets.Label(new Rect(rowRect.x + 560f, rowRect.y + 28f, 180f, 22f), $"Total: {lot.TotalPrice:F0} Silver");
                GUI.color = Color.white;

                // Col 4: Action
                bool isMine = lot.SellerLogin.Equals(playerName, StringComparison.OrdinalIgnoreCase);
                if (isMine)
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 140f, rowRect.y + 11f, 130f, 32f), "Cancel Listing"))
                    {
                        gov.MarketLots.Remove(lot);
                        Messages.Message($"[MARKETPLACE] Listing for {lot.StackCount}x {lot.ItemLabel} cancelled.", MessageTypeDefOf.TaskCompletion);
                    }
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(rowRect.xMax - 140f, rowRect.y + 11f, 130f, 32f), "Purchase Lot"))
                    {
                        Find.WindowStack.Add(new Dialog_MarketPurchase(lot, Find.CurrentMap));
                    }
                }

                curY += 58f;
            }

            Widgets.EndScrollView();
        }
        #endregion
    }
}
