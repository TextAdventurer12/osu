// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Skills;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class RawAim : ProportionateStrainSkill
    {
        protected override double k => 2;
        protected override double multiplier => 1;
        /// <summary>
        /// TODO: remove difficulties where it is easier to flow aim
        /// </summary>
        protected override void CalculateDifficulties()
        {
            return;
        }
        protected override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            currentDifficulty = WeirdAimEvaluator.EvaluateDifficultyOf(current);
            difficulties.Add(StrainValueAt(current));
        }
    }
}