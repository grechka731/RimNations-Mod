using System;
using System.Collections.Generic;
using System.Linq;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorldOnlineCity;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    public class Dialog_CreateMarketListing : Window
    {
        private readonly Map map;
        private Thing selectedThing = null;
        private int selectedCount = 1;
        private string pricePerUnitStr = "10";
        private MarketCategory category = MarketCategory.Resources;

        public override Vector2 InitialSize => new Vector2(520f, 480f);

        public Dialog_CreateMarketListing(Map map)
        {
            this.map = map ?? Find.CurrentMap;
            doCloseButton = false;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Post Commodity Lot to Global Exchange");
            Text.Font = GameFont.Small;
            listing.GapLine();

            if (map == null)
            {
                listing.Label("No active colony map selected.");
                listing.End();
                return;
            }

            var tradeableThings = map.listerThings.AllThings
                .Where(t => t.def.category == ThingCategory.Item && !t.Position.Fogged(map) && t.stackCount > 0)
                .GroupBy(t => t.def)
                .Select(g => g.First())
                .ToList();

            string thingLabel = selectedThing != null ? $"{selectedThing.LabelCap} (Available: {map.listerThings.ThingsOfDef(selectedThing.def).Sum(t => t.stackCount)})" : "[Select Item from Stockpile]";
            listing.Label($"Selected Item: {thingLabel}");

            if (listing.ButtonText("Browse Colony Stockpiles"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var th in tradeableThings)
                {
                    var item = th;
                    int totalInMap = map.listerThings.ThingsOfDef(item.def).Sum(t => t.stackCount);
                    options.Add(new FloatMenuOption($"{item.LabelCap} x{totalInMap}", () =>
                    {
                        selectedThing = item;
                        selectedCount = Math.Min(selectedCount, totalInMap);
                        if (selectedCount <= 0) selectedCount = 1;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (selectedThing != null)
            {
                int maxAvailable = map.listerThings.ThingsOfDef(selectedThing.def).Sum(t => t.stackCount);
                listing.Gap(6f);
                listing.Label($"Quantity to List: {selectedCount} / {maxAvailable}");
                selectedCount = Mathf.RoundToInt(listing.Slider(selectedCount, 1, maxAvailable));

                listing.Gap(4f);
                listing.Label("Price Per Unit in Silver:");
                pricePerUnitStr = listing.TextEntry(pricePerUnitStr);

                listing.Gap(4f);
                listing.Label($"Market Category: {category}");
                if (listing.ButtonText("Select Category"))
                {
                    List<FloatMenuOption> catOptions = new List<FloatMenuOption>();
                    foreach (MarketCategory c in Enum.GetValues(typeof(MarketCategory)))
                    {
                        if (c == MarketCategory.All) continue;
                        MarketCategory cat = c;
                        catOptions.Add(new FloatMenuOption(cat.ToString(), () => category = cat));
                    }
                    Find.WindowStack.Add(new FloatMenu(catOptions));
                }

                if (float.TryParse(pricePerUnitStr, out float unitPrice) && unitPrice > 0f)
                {
                    float totalExpected = selectedCount * unitPrice;
                    listing.Gap(6f);
                    GUI.color = Color.green;
                    listing.Label($"Total Gross Listing Value: {totalExpected:F0} Silver");
                    GUI.color = Color.white;
                }
            }

            listing.Gap(16f);

            // Action Buttons
            Rect bottomRect = new Rect(0, inRect.height - 40f, inRect.width, 36f);
            float btnWidth = (bottomRect.width - 20f) / 2f;

            bool canList = selectedThing != null && float.TryParse(pricePerUnitStr, out float price) && price > 0f && selectedCount > 0;
            if (!canList) GUI.color = Color.gray;

            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, btnWidth, bottomRect.height), "Publish Listing") && canList)
            {
                float.TryParse(pricePerUnitStr, out float parsedPrice);
                int needed = selectedCount;

                var thingsOfDef = map.listerThings.ThingsOfDef(selectedThing.def).ToList();
                foreach (var t in thingsOfDef)
                {
                    if (needed <= 0) break;
                    int take = Math.Min(t.stackCount, needed);
                    t.SplitOff(take).Destroy();
                    needed -= take;
                }

                var gov = WorldComponent_StateGovernor.Instance;
                string seller = SessionClientController.My?.Login ?? "Sovereign";
                string state = SessionClientController.My?.StateName ?? "Local Realm";

                var lot = new MarketTradeLot(seller, state, selectedThing.def.defName, selectedThing.LabelCap, selectedCount, parsedPrice, category);
                gov?.MarketLots.Add(lot);

                Messages.Message($"[MARKETPLACE] Listed {selectedCount}x {selectedThing.LabelCap} for {parsedPrice:F1} Silver/ea.", MessageTypeDefOf.PositiveEvent);
                Close();
            }
            GUI.color = Color.white;

            if (Widgets.ButtonText(new Rect(bottomRect.x + btnWidth + 20f, bottomRect.y, btnWidth, bottomRect.height), "Cancel"))
            {
                Close();
            }

            listing.End();
        }
    }
}
