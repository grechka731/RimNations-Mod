using System;
using RimNations.Core.GrandStrategy.Core;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    public class Dialog_MarketPurchase : Window
    {
        private readonly MarketTradeLot lot;
        private readonly Map targetMap;

        public override Vector2 InitialSize => new Vector2(500f, 380f);

        public Dialog_MarketPurchase(MarketTradeLot lot, Map targetMap)
        {
            this.lot = lot;
            this.targetMap = targetMap ?? Find.CurrentMap;

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
            listing.Label("Finalize Commodity Purchase");
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Label($"Commodity: {lot.StackCount}x {lot.ItemLabel}");
            listing.Label($"Seller: {lot.SellerLogin} ({lot.SellerStateName})");
            listing.Label($"Item Cost: {lot.TotalPrice:F0} Silver ({lot.UnitPrice:F1} Silver/ea)");
            listing.GapLine();

            listing.Label("Select Logistics & Delivery Protocol:");
            listing.Gap(6f);

            var gov = WorldComponent_StateGovernor.Instance;
            float fee = lot.TotalPrice * 0.10f;
            float podTotal = lot.TotalPrice + fee;

            // Option 1: Express Drop Pod
            GUI.color = (gov != null && gov.NationalTreasurySilver >= podTotal) ? Color.cyan : Color.gray;
            if (listing.ButtonText($"1. Express Drop-Pod Delivery (+10% fee: {podTotal:F0} Silver total)"))
            {
                if (gov != null && gov.PurchaseMarketLot(lot, expressDropPod: true, targetMap))
                {
                    Close();
                }
            }
            GUI.color = Color.white;
            listing.Label("   • Launches orbital transport pods directly adjacent to your trade beacon.");
            listing.Gap(8f);

            // Option 2: Warehouse Stockpile
            GUI.color = (gov != null && gov.NationalTreasurySilver >= lot.TotalPrice) ? Color.green : Color.gray;
            if (listing.ButtonText($"2. Warehouse Logistics Delivery ({lot.TotalPrice:F0} Silver total)"))
            {
                if (gov != null && gov.PurchaseMarketLot(lot, expressDropPod: false, targetMap))
                {
                    Close();
                }
            }
            GUI.color = Color.white;
            listing.Label("   • Securely transfers goods to designated colony storage shelves / stockpiles.");
            listing.Gap(12f);

            if (listing.ButtonText("Cancel Order"))
            {
                Close();
            }

            listing.End();
        }
    }
}
