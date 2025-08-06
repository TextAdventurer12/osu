// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;
        private double currentAgilityStrain;
        private bool? previousStrainAimType;

        private double snapMultiplier => 27.5;
        private double flowMultiplier => 35;
        private double agilityMultiplier => 0.45;
        private double strainDecayBase => 0.15;
        private double agilityStrainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * agilityMultiplier;
            double baseSnapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * snapMultiplier;
            double snapDifficulty = baseSnapDifficulty + agilityDifficulty + currentAgilityStrain;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current) * flowMultiplier;

            double transitionBonus = 1.0;

            bool currentStrainAimType = flowDifficulty < snapDifficulty;

            if (previousStrainAimType is not null && currentStrainAimType != previousStrainAimType)
            {
                transitionBonus = 0.25 * DifficultyCalculationUtils.Smootherstep(Math.Abs(snapDifficulty - flowDifficulty), 0.3, 10);

                // Going from flow to snap is harder than going from snap to flow
                transitionBonus = currentStrainAimType ? 1 + transitionBonus : 1 + 2 * transitionBonus;
            }

            snapDifficulty *= currentStrainAimType ? 1 : transitionBonus;
            flowDifficulty *= currentStrainAimType ? transitionBonus : 1;

            currentStrainAimType = flowDifficulty < snapDifficulty;

            previousStrainAimType = currentStrainAimType;

            double currentDifficulty = currentStrainAimType ? flowDifficulty : snapDifficulty;

            currentStrain += currentDifficulty;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
