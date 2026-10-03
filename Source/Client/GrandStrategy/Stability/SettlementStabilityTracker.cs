using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.Stability
{
    public enum RevoltStage
    {
        Peaceful = 0,
        GeneralStrike = 1,
        PalaceCoup = 2,
        Secession = 3
    }

    public class SettlementStabilityTracker : IExposable
    {
        public int SettlementTile;
        public string SettlementName = "Colony";

        // Unrest ranges from 0.0f (complete harmony) to 100.0f (revolution)
        public float Unrest = 10.0f;
        public RevoltStage CurrentStage = RevoltStage.Peaceful;

        // Governor influence
        public string GovernorName = null;
        public float TaxRate = 0.15f; // 15% standard tribute
        public bool HasGarrison = true;
        public int GarrisonSize = 4;

        // Logging reasons for player transparency
        public List<string> RecentGrievances = new List<string>();

        public SettlementStabilityTracker() { }

        public SettlementStabilityTracker(int tile, string name)
        {
            SettlementTile = tile;
            SettlementName = name;
            Unrest = 10.0f;
            CurrentStage = RevoltStage.Peaceful;
        }

        public void AddGrievance(float amount, string reason)
        {
            Unrest = Mathf.Clamp(Unrest + amount, 0.0f, 100.0f);
            string entry = $"[{DateTime.UtcNow:HH:mm}] +{amount:F1}% Unrest: {reason}";
            RecentGrievances.Insert(0, entry);
            if (RecentGrievances.Count > 10) RecentGrievances.RemoveAt(RecentGrievances.Count - 1);

            UpdateRevoltStage();
        }

        public void ReduceUnrest(float amount, string reason)
        {
            Unrest = Mathf.Clamp(Unrest - amount, 0.0f, 100.0f);
            string entry = $"[{DateTime.UtcNow:HH:mm}] -{amount:F1}% Unrest: {reason}";
            RecentGrievances.Insert(0, entry);
            if (RecentGrievances.Count > 10) RecentGrievances.RemoveAt(RecentGrievances.Count - 1);

            UpdateRevoltStage();
        }

        public void MacroTickStability(float deltaDays, Pawn governor = null, RimNations.Core.GrandStrategy.Economics.ProvincialArchetypeDef archetype = null)
        {
            // Passive decay or increase based on governance factors
            float dailyChange = 0.0f;

            // Archetype unrest modifier
            if (archetype != null)
            {
                dailyChange += archetype.BaseUnrestPerDay;
            }

            // Governor persona traits & skill modifiers
            if (governor != null)
            {
                if (governor.story != null && governor.story.traits != null)
                {
                    // Greedy: +1.5% daily Unrest (extortion)
                    if (governor.story.traits.HasTrait(TraitDefOf.Greedy))
                    {
                        dailyChange += 1.5f;
                    }
                    // Kind: -1.0% daily Unrest
                    if (governor.story.traits.HasTrait(TraitDefOf.Kind))
                    {
                        dailyChange -= 1.0f;
                    }
                    // Abrasive: +2.0% daily Unrest (sparks labor disputes)
                    if (governor.story.traits.HasTrait(TraitDefOf.Abrasive))
                    {
                        dailyChange += 2.0f;
                    }
                }

                // Social Skill: Every 5 levels reduces daily Unrest by 0.5%
                if (governor.skills != null)
                {
                    int socialLevel = governor.skills.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                    float socialReduction = (socialLevel / 5) * 0.5f;
                    dailyChange -= socialReduction;
                }
            }

            // High taxation increases unrest
            if (TaxRate > 0.25f)
            {
                dailyChange += (TaxRate - 0.25f) * 15.0f; // Excessive taxation breeds anger
            }
            else if (TaxRate < 0.10f)
            {
                dailyChange -= 3.0f; // Low taxes appease the citizenry
            }

            // Garrison suppression
            if (HasGarrison && GarrisonSize > 0)
            {
                dailyChange -= Mathf.Min(GarrisonSize * 0.8f, 6.0f);
            }

            Unrest = Mathf.Clamp(Unrest + (dailyChange * deltaDays), 0.0f, 100.0f);
            UpdateRevoltStage();
        }

        private void UpdateRevoltStage()
        {
            RevoltStage previous = CurrentStage;

            if (Unrest >= 90.0f)
            {
                CurrentStage = RevoltStage.Secession;
            }
            else if (Unrest >= 75.0f)
            {
                CurrentStage = RevoltStage.PalaceCoup;
            }
            else if (Unrest >= 50.0f)
            {
                CurrentStage = RevoltStage.GeneralStrike;
            }
            else
            {
                CurrentStage = RevoltStage.Peaceful;
            }

            if (CurrentStage != previous)
            {
                NotifyRevoltStageChanged(previous, CurrentStage);
            }
        }

        private void NotifyRevoltStageChanged(RevoltStage oldStage, RevoltStage newStage)
        {
            switch (newStage)
            {
                case RevoltStage.GeneralStrike:
                    Messages.Message($"[ВОЛНЕНИЯ] В поселении {SettlementName} началась всеобщая забастовка (Недовольство: {Unrest:F0}%)! Жители прекратили абстрактное производство и отправку дани.",
                        MessageTypeDefOf.ThreatSmall);
                    break;
                case RevoltStage.PalaceCoup:
                    Messages.Message($"[МЯТЕЖ] В поселении {SettlementName} произошел дворцовый переворот (Недовольство: {Unrest:F0}%)! Губернатор низложен жителями. Требуется срочное снижение налогов.",
                        MessageTypeDefOf.ThreatBig);
                    break;
                case RevoltStage.Secession:
                    Messages.Message($"[СЕЦЕССИЯ / ВОССТАНИЕ!] Поселение {SettlementName} официально вышло из состава государства (Недовольство: {Unrest:F0}%)! В городе поднят повстанческий флаг!",
                        MessageTypeDefOf.ThreatBig);
                    break;
                case RevoltStage.Peaceful:
                    if (oldStage > RevoltStage.Peaceful)
                    {
                        Messages.Message($"[ПОРЯДОК ВОССТАНОВЛЕН] В поселении {SettlementName} восстановлен общественный порядок и лояльность.",
                            MessageTypeDefOf.PositiveEvent);
                    }
                    break;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref SettlementTile, "SettlementTile");
            Scribe_Values.Look(ref SettlementName, "SettlementName", "Colony");
            Scribe_Values.Look(ref Unrest, "Unrest", 10.0f);
            Scribe_Values.Look(ref CurrentStage, "CurrentStage", RevoltStage.Peaceful);
            Scribe_Values.Look(ref GovernorName, "GovernorName");
            Scribe_Values.Look(ref TaxRate, "TaxRate", 0.15f);
            Scribe_Values.Look(ref HasGarrison, "HasGarrison", true);
            Scribe_Values.Look(ref GarrisonSize, "GarrisonSize", 4);
            Scribe_Collections.Look(ref RecentGrievances, "RecentGrievances", LookMode.Value);
        }
    }
}
