// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class WeirdAimEvaluator
    {
        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index < 2 || current.Previous(0).BaseObject is Spinner)
                return 0;

            // raw aim difficulty is linear with difficulty
            return current.Travel.Length / current.StrainTime * Math.Pow(Math.Sin(Math.Clamp(1.2 * current.Angle.Value - Math.PI / 4.0, 0, Math.PI / 2)), 2);;
        }
    }
}