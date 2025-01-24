// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Skills;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuSkill : Skill
    {
        public OsuSkill(Mod[] mods)
            : base(Mods)
        {

        }
        protected List<double> difficulties;
        /// <summary>
        /// weighting factor for object summation
        /// lower k = long maps worth more
        /// </summary>
        protected abstract double k;
        protected abstract double multiplier;

        public override double DifficultyValue()
        {
            CalculateDifficulties();
            return Math.Pow(difficulties.Sum(difficulty => Math.Pow(difficulty, k)), 1 / k) * multiplier;
        }
        /// <summary>
        /// Called just before difficulty is summed
        /// By the end of this function, difficulties should be populated
        /// This exists alongside Process() (which may also be used to populate difficulties) to allow for skills to use non-object based difficulties
        /// </summary>
        protected virtual void CalculateDifficulties();
    }
}