// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrevPrevObj = (OsuDifficultyHitObject)current.Previous(1);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.AdjustedDeltaTime;

            double difficulty = Math.Pow(osuCurrObj.LazyJumpDistance, 1.8) * 0.1;

            // Nerf isolated direction changes
            double directionChangeFactor = Math.Max(0,
                angleChangeCount(osuCurrObj, osuPrevObj) - 0.7 * Math.Abs(angleChangeCount(osuCurrObj, osuPrevObj) - angleChangeCount(osuPrevObj, osuPrevPrevObj)));

            directionChangeFactor = Math.Pow(directionChangeFactor, currVelocity) * 100;

            difficulty += directionChangeFactor;

            return difficulty / osuCurrObj.AdjustedDeltaTime * 1.2;
        }

        private static double angleChangeCount(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuPrevObj)
        {
            double directionChangeFactor = 0;

            if (osuCurrObj.AngleSigned.IsNotNull() && osuPrevObj.AngleSigned.IsNotNull() && osuCurrObj.Angle.IsNotNull())
            {
                double anglDifference = Math.Abs(osuCurrObj.AngleSigned.Value - osuPrevObj.AngleSigned.Value);

                // Account for the fact that you can aim patterns in a straight line
                anglDifference *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));

                directionChangeFactor += anglDifference;
            }

            return directionChangeFactor;
        }
    }
}
