// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuStrainSkill : OsuSkill
    {
        protected OsuStrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected double CurrentDifficulty;
        private double currentStrain;
        protected abstract double StrainDecayBase { get; }
        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);

        protected double StrainValueAt(OsuDifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += CurrentDifficulty;

            return currentStrain;
        }
    }
}
