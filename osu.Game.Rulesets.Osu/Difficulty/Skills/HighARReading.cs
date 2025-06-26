// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class HighARReading : OsuStrainSkill
    {
        private double preempt;
        public HighARReading(Mod[] mods, IBeatmap beatmap, double clockRate)
            : base(mods)
        {
            preempt = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / clockRate;
        }

        private double currentStrain;
        private double skillMultiplier => 2.0;
        private double strainDecayBase => 0.6;
        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += HighARReadingEvaluator.EvaluateDifficultyOf(current, preempt) * skillMultiplier;

            return currentStrain;
        }
    }
}