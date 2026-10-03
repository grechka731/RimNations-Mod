using System;
using System.Collections.Generic;
using RimNations.Core.GrandStrategy.Stability;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.Economics
{
    public static class MacroYieldMath
    {
        public const float BaseNutritionPerColonistPerDay = 1.6f;

        /// <summary>
        /// Вычисляет абстрактное производство ресурсов за интервал времени:
        /// DeltaResource = SkillFactor * WorkTypeMultiplier * PawnCount * DeltaDays * ArchetypeBonus * GovernorMultiplier
        /// </summary>
        public static float CalculateProduction(
            int pawnCount,
            float averageSkill,
            ProvincialArchetypeDef archetype,
            Pawn governor,
            RevoltStage revoltStage,
            float deltaDays)
        {
            if (pawnCount <= 0 || deltaDays <= 0) return 0f;

            // Strike penalties per Section 2:
            // 0-49% Stable: 100% output
            // 50-74% General Strike: 50% output (penalty 50%)
            // 75-89% Palace Coup: 20% output (penalty 80%)
            // 90-100% Armed Secession: 0% output
            float strikeFactor = 1.0f;
            if (revoltStage == RevoltStage.Secession) return 0f;
            else if (revoltStage == RevoltStage.PalaceCoup) strikeFactor = 0.20f;
            else if (revoltStage == RevoltStage.GeneralStrike) strikeFactor = 0.50f;

            // Skill factor normalized around level 10 (0.5 at lvl 0, 1.0 at lvl 10, 1.5 at lvl 20)
            float skillFactor = Mathf.Clamp(0.5f + (averageSkill * 0.05f), 0.5f, 2.0f);

            // Governor personality modifier per Section 1:
            // Greedy: +20% output
            // Kind: -15% taxes/yield extraction
            float governorMultiplier = 1.0f;
            if (governor != null && governor.story != null && governor.story.traits != null)
            {
                if (governor.story.traits.HasTrait(TraitDefOf.Industriousness))
                {
                    governorMultiplier += 0.20f;
                }
                if (governor.story.traits.HasTrait(TraitDefOf.Greedy))
                {
                    governorMultiplier += 0.20f; // Squeezes populace for +20% output
                }
                if (governor.story.traits.HasTrait(TraitDefOf.Kind))
                {
                    governorMultiplier -= 0.15f; // Benevolent, lenient quotas
                }
            }

            float archetypeMult = archetype != null ? archetype.ProductionMultiplier : 1.0f;

            // Base rate: 10 units per skilled pawn per day
            float baseDailyOutput = 10f;
            float totalYield = baseDailyOutput * skillFactor * archetypeMult * governorMultiplier * strikeFactor * pawnCount * deltaDays;

            return totalYield;
        }

        /// <summary>
        /// Вычисляет потребление продовольствия колонией в оффлайн-режиме
        /// </summary>
        public static float CalculateFoodConsumption(int pawnCount, float deltaDays)
        {
            return pawnCount * BaseNutritionPerColonistPerDay * deltaDays;
        }
    }
}
