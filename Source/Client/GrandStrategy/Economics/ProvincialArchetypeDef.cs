using System;
using Verse;

namespace RimNations.Core.GrandStrategy.Economics
{
    public class ProvincialArchetypeDef : Def
    {
        public float ProductionMultiplier = 1.0f;
        public float FoodYieldMultiplier = 1.0f;
        public float MineralYieldMultiplier = 1.0f;
        public float SilverYieldMultiplier = 1.0f;

        public float BaseUnrestPerDay = 0.0f;
        public float DefenseStrengthMultiplier = 1.0f;
        public float TradeTariffMultiplier = 1.0f;

        public string PrimaryProductDefName = "RawPotatoes";
        public int DefaultDailyYield = 100;
    }
}
