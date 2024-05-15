// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuProbSkill
    {
        public Aim(Mod[] mods, double radius, bool withSliders)
            : base(mods, radius)
        {
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private double currentStrain;

        protected override double skillMultiplier => 12500;
        private double strainDecayBase => 0.15;

        protected override double FcProbability => 0.02;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double DeviationAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, withSliders) * skillMultiplier;

            return currentStrain;
        }
    }
}