// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public abstract class Aim : OsuContaminatedProbabilitySkill
    {
        public readonly bool IncludeSliders;
        private readonly IReadOnlyList<HitObject> objectList;
        private readonly double clockRate;
        private readonly bool hasHiddenMod;
        private readonly double preempt;

        protected Aim(IBeatmap beatmap, Mod[] mods, double clockRate, bool includeSliders)
            : base(mods)
        {
            this.clockRate = clockRate;
            hasHiddenMod = mods.OfType<OsuModHidden>().Any(m => !m.OnlyFadeApproachCircles.Value);
            preempt = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            IncludeSliders = includeSliders;
            objectList = beatmap.HitObjects;
        }

        private double currentStrain;
        private double currentReading;

        private double skillMultiplier => 167.27;
        private double strainDecayBase => 0.15;
        // contribution of the reading distribution to difficulty
        protected override double alpha => 0.01;
        private double readingMultiplier => 0.2;

        private readonly List<double> sliderStrains = new List<double>();

        protected override double HitProbability(double skill, double difficulty, double lambda)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            double p_main = DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
            double p_contam = (lambda <= 0 ? 1 : DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty * lambda)));

            // contaminated normal distribution with a weighting factor of lambda, where one distribution has deviation skill / difficulty and the other has a multiplier of readingDifficulty
            return (1 - alpha) * p_main + alpha * p_contam;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected abstract double StrainValueOf(DifficultyHitObject current);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);

            currentStrain += StrainValueOf(current) * skillMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        protected override double LambdaValueAt(DifficultyHitObject current)
        {
            currentReading *= Math.Pow(0.8, current.DeltaTime / 1000);

            currentReading += ReadingEvaluator.EvaluateDifficultyOf(objectList.Count, current, clockRate, preempt, hasHiddenMod) * readingMultiplier;

            return currentReading;
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
