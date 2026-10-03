using System;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    /// <summary>
    /// Worker Beta / Gamma: Customs & Toll Gateways modal dialog.
    /// Intercepts caravans entering sovereign territory, presenting three options:
    /// 1. Pay Toll: Deducts configured rate (0-20% of cargo value) and credits sovereign treasury.
    /// 2. Turn Back: Retreats to neutral territory outside the border.
    /// 3. Breach Border: Initiates an international border incident / Casus Belli.
    /// </summary>
    public class Dialog_BorderCustoms : Window
    {
        private readonly Caravan caravan;
        private readonly int entryTile;
        private readonly float cargoValue;
        private readonly float tariffRate;
        private readonly float calculatedToll;
        private readonly string sovereignNationName;

        public override Vector2 InitialSize => new Vector2(580f, 420f);

        public Dialog_BorderCustoms(Caravan caravan, int entryTile, float cargoValue, float tariffRate, string nationName)
        {
            this.caravan = caravan;
            this.entryTile = entryTile;
            this.cargoValue = cargoValue;
            this.tariffRate = tariffRate;
            this.calculatedToll = (float)Math.Round(cargoValue * tariffRate);
            this.sovereignNationName = nationName ?? "Sovereign Territory";

            doCloseButton = false;
            doCloseX = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            GUI.color = Color.yellow;
            listing.Label("INTERNATIONAL BORDER CUSTOMS & TRANSIT CHECKPOINT");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.GapLine();

            string caravanName = caravan?.LabelCap ?? "Caravan";
            listing.Label($"Caravan: {caravanName} (Entering Tile #{entryTile})");
            listing.Label($"Territorial Jurisdiction: {sovereignNationName}");
            listing.Label($"Assessed Cargo Market Value: {cargoValue:F0} Silver");
            listing.Label($"Customs Transit Tariff: {(tariffRate * 100f):F1}%");

            GUI.color = Color.green;
            listing.Label($"Required Transit Toll: {calculatedToll:F0} Silver");
            GUI.color = Color.white;
            listing.GapLine();

            listing.Label("Select Border Resolution Protocol:");
            listing.Gap(6f);

            // OPTION 1: PAY TOLL
            if (listing.ButtonText($"1. Pay Toll ({calculatedToll:F0} Silver) & Pass Legally"))
            {
                var gov = WorldComponent_StateGovernor.Instance;
                if (gov != null)
                {
                    gov.NationalTreasurySilver += calculatedToll;
                }
                Messages.Message($"[BORDER CUSTOMS] {caravanName} paid {calculatedToll:F0} silver in transit tolls and entered legally.", MessageTypeDefOf.PositiveEvent);
                Close();
            }

            listing.Gap(4f);

            // OPTION 2: TURN BACK
            if (listing.ButtonText("2. Turn Back to Neutral Territory"))
            {
                if (caravan != null && caravan.pather != null)
                {
                    caravan.pather.StopDead();
                }
                Messages.Message($"[BORDER CUSTOMS] {caravanName} refused the customs toll and retreated to neutral territory.", MessageTypeDefOf.NeutralEvent);
                Close();
            }

            listing.Gap(4f);

            // OPTION 3: BREACH BORDER
            GUI.color = Color.red;
            if (listing.ButtonText("3. Breach Sovereign Border (Force Transit / Casus Belli)"))
            {
                var gov = WorldComponent_StateGovernor.Instance;
                var tracker = gov?.GetOrCreateStabilityTracker(entryTile, sovereignNationName);
                tracker?.AddGrievance(10.0f, $"Illegal border breach by foreign caravan: {caravanName}");

                Messages.Message($"[INTERNATIONAL INCIDENT] {caravanName} forcefully breached sovereign borders! A legitimate Casus Belli has been generated.", MessageTypeDefOf.ThreatBig);
                Close();
            }
            GUI.color = Color.white;

            listing.End();
        }
    }
}
