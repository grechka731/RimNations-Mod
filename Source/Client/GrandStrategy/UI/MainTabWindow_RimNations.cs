using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.UI
{
    /// <summary>
    /// Worker Gamma: MainTabWindow for RimNations tray button (Hotkey 'U').
    /// Provides zero-chat-command native access to the geopolitical engine dashboard.
    /// </summary>
    public class MainTabWindow_RimNations : MainTabWindow
    {
        public override Vector2 InitialSize => new Vector2(960f, 680f);

        public override void PreOpen()
        {
            base.PreOpen();
            // Opens the central RimNations dashboard window
            if (!Find.WindowStack.IsOpen<Dialog_RimNationsDashboard>())
            {
                Find.WindowStack.Add(new Dialog_RimNationsDashboard());
            }
            Close(false);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // The central UI is rendered in Dialog_RimNationsDashboard
        }
    }
}
