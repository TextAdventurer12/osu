// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class FlowAim : Aim
    {
        public FlowAim(IBeatmap beatmap, Mod[] mods, double clockRate)
            : base(beatmap, mods, clockRate, false)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double snap = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flow = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);

            if (snap < flow)
                return snap * Math.Pow(snap / flow, 2.5);

            return flow;
        }
    }
}
