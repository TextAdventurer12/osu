// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuSkill : Skill
    {
        protected OsuSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected List<double> Difficulties = new List<double>();

        /// <summary>
        /// weighting factor for object summation
        /// lower k = long maps worth more
        /// </summary>
        protected abstract double K { get; }

        protected abstract double Multiplier { get; }

        public override double DifficultyValue()
        {
            CalculateDifficulties();
            return Math.Pow(Difficulties.Sum(difficulty => Math.Pow(difficulty, K)), 1 / K) * Multiplier;
        }

        /// <summary>
        /// Called just before difficulty is summed
        /// By the end of this function, difficulties should be populated
        /// This exists alongside Process() (which may also be used to populate difficulties) to allow for skills to use non-object based difficulties
        /// </summary>
        protected abstract void CalculateDifficulties();
    }
}
