// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class CombinedAim : Aim
    {
        public CombinedAim(IBeatmap beatmap, Mod[] mods, double clockRate, bool includeSliders)
            : base(beatmap, mods, clockRate, includeSliders)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double snap = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flow = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);

            return Math.Min(snap, flow);
        }
    }
}
