// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : OsuProbSkill
    {
        protected override double skillMultiplier => 80000000000000;
        protected override double FcProbability => 0.02;
        private double strainDecayBase => 0.3;

        private double currentStrain;
        private double currentRhythm;

        public Speed(Mod[] mods, double radius)
            : base(mods, radius)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double DeviationAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            return Math.Cbrt(totalStrain);
        }
    }
}