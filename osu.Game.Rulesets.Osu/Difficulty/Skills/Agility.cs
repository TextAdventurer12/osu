// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Skills;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class Agility : OsuStrainSkill
    {
        protected override double k => 2;
        protected override double multiplier => 1;
        /// <summary>
        /// TODO: remove all difficulties were it is easier to use flow aim
        /// </summary>
        protected override void CalculateDifficulties()
        {
            return;
        }
        protected override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            currentDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current);
            difficulties.Add(StrainValueAt(current));
        }
    }
}