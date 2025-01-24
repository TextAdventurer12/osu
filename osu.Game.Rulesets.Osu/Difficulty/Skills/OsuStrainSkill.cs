// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuStrainSkill : OsuSkill
    {
        protected OsuStrainSkill(Mod[] mods)
            : base(mods)
        {
        }
        protected double currentDifficulty;
        private double currentStrain;
        protected double strainDecayBase;
        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        protected double StrainValueAt(OsuDifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += currentDifficulty;

            return currentStrain;
        }
    }
}