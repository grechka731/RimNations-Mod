using System;
using System.Collections.Generic;
using RimNations.Core.GrandStrategy.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    public class Dialog_EditPosition : Window
    {
        private readonly MinistryOffice office;
        private readonly bool isNew;
        private string titleBuffer;
        private string assignedLoginBuffer;

        public override Vector2 InitialSize => new Vector2(480f, 440f);

        public Dialog_EditPosition(MinistryOffice office, bool isNew = false)
        {
            this.office = office;
            this.isNew = isNew;
            this.titleBuffer = office.Title;
            this.assignedLoginBuffer = office.AssignedPlayerLogin;

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
            listing.Label(isNew ? "Form New Ministry / Office" : "Edit Government Position");
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Label("Position Title (e.g. Minister of War, Grand Chancellor):");
            titleBuffer = listing.TextEntry(titleBuffer);
            listing.Gap(6f);

            // Citizen Assignment
            var gov = WorldComponent_StateGovernor.Instance;
            string displayAssignee = string.IsNullOrEmpty(assignedLoginBuffer) ? "[Unassigned / Vacant]" : assignedLoginBuffer;
            listing.Label($"Assigned Citizen: {displayAssignee}");

            if (listing.ButtonText("Select Citizen"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                options.Add(new FloatMenuOption("[Vacant]", () => assignedLoginBuffer = ""));

                if (gov != null)
                {
                    foreach (var c in gov.Citizens)
                    {
                        string cLogin = c.PlayerLogin;
                        options.Add(new FloatMenuOption(cLogin, () => assignedLoginBuffer = cLogin));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.GapLine();
            listing.Label("Constitutional Permissions & Executive Powers:");
            listing.Gap(4f);

            listing.CheckboxLabeled("Can Declare War", ref office.CanDeclareWar, "Allows issuing declarations of war and mobilizing battalions.");
            listing.CheckboxLabeled("Can Authorize Peace & Treaties", ref office.CanAuthorizePeace, "Allows signing non-aggression pacts, alliances, and peace treaties.");
            listing.CheckboxLabeled("Can Invite / Banish Citizens", ref office.CanInviteBanishCitizens, "Allows admitting new vassal states or revoking citizen status.");
            listing.CheckboxLabeled("Can Adjust Customs Tariffs", ref office.CanAdjustCustomsTariff, "Allows changing border tariff percentages and border blockades.");
            listing.CheckboxLabeled("Access to National Treasury", ref office.CanAccessNationalTreasury, "Allows withdrawing silver reserves from state treasury.");

            listing.Gap(12f);

            // Bottom Buttons
            Rect bottomRect = new Rect(0, inRect.height - 40f, inRect.width, 36f);
            float btnWidth = (bottomRect.width - 20f) / 2f;

            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, btnWidth, bottomRect.height), "Save Position"))
            {
                office.Title = string.IsNullOrEmpty(titleBuffer) ? "Minister" : titleBuffer.Trim();
                office.AssignedPlayerLogin = assignedLoginBuffer;

                if (isNew && gov != null)
                {
                    gov.MinistryOffices.Add(office);
                }

                Messages.Message($"[GOVERNMENT] Office '{office.Title}' registered with sovereign credentials.", MessageTypeDefOf.TaskCompletion);
                Close();
            }

            if (!isNew)
            {
                if (Widgets.ButtonText(new Rect(bottomRect.x + btnWidth + 20f, bottomRect.y, btnWidth, bottomRect.height), "Dissolve Office"))
                {
                    gov?.MinistryOffices.Remove(office);
                    Messages.Message($"[GOVERNMENT] Office '{office.Title}' dissolved.", MessageTypeDefOf.CautionInput);
                    Close();
                }
            }
            else
            {
                if (Widgets.ButtonText(new Rect(bottomRect.x + btnWidth + 20f, bottomRect.y, btnWidth, bottomRect.height), "Cancel"))
                {
                    Close();
                }
            }

            listing.End();
        }
    }
}
