// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 2 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);

            double time = current.StrainTime;
            double distance = Math.Min(current.Travel.Length, osuLastObj.Travel.Length) - diameter;

            double currAngle = current.Angle.Value;
            double lastAngle = osuLastObj.Angle.Value;

            return Math.Sqrt(distance) / time
                        * DifficultyCalculationUtils.Smootherstep(currAngle, double.DegreesToRadians(110), double.DegreesToRadians(60))
                        * DifficultyCalculationUtils.Smootherstep(lastAngle, double.DegreesToRadians(110), double.DegreesToRadians(60))
                        * Math.Sin(currAngle / 2);
        }
    }
}