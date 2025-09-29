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
            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);

            double currDistanceDifference = Math.Abs(osuCurrObj.MinimumJumpDistance - osuPrevObj.MinimumJumpDistance);
            double prevDistanceDifference = Math.Abs(osuPrevObj.MinimumJumpDistance - osuPrev2Obj.MinimumJumpDistance);

            double jerk = Math.Abs(currDistanceDifference - prevDistanceDifference);

            double angleDifferenceAdjusted = Math.Sin(directionChange(osuCurrObj, osuPrevObj) / 2) * 180;

            var osuNextObj = (OsuDifficultyHitObject)current.Next(0);
            var osuPrev3Obj = (OsuDifficultyHitObject)current.Previous(2);

            // Nerf the last note of spaced triples as its angle is not representative of its flow difficulty
            if (osuPrev3Obj.IsNotNull() && Math.Abs(osuPrev2Obj.AdjustedDeltaTime - osuPrev3Obj.AdjustedDeltaTime) > 25 &&
                osuNextObj.IsNotNull() && Math.Abs(osuCurrObj.AdjustedDeltaTime - osuNextObj.AdjustedDeltaTime) > 25 && osuCurrObj.Angle.IsNotNull())
            {
                angleDifferenceAdjusted *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));
                jerk *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));
            }

            double angularChangeBonus = Math.Max(0.0, 0.6 * Math.Log10(angleDifferenceAdjusted));

            double adjustedDistanceScale = 0.85 + Math.Min(1, jerk / 15) + angularChangeBonus * Math.Clamp(jerk / 15, 0.5, 1);

            // Value distance exponentially, and scale with direction and distance changes
            double distanceFactor = Math.Pow(osuCurrObj.LazyJumpDistance, 2) * adjustedDistanceScale;

            double difficulty = distanceFactor / osuCurrObj.AdjustedDeltaTime;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.03;
        }

        private static double directionChange(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuPrevObj)
        {
            double directionChangeFactor = 0;

            if (osuCurrObj.AngleSigned.IsNotNull() && osuPrevObj.AngleSigned.IsNotNull() &&
                osuCurrObj.Angle.IsNotNull() && osuPrevObj.Angle.IsNotNull())
            {
                double signedAngleDifference = Math.Abs(osuCurrObj.AngleSigned.Value - osuPrevObj.AngleSigned.Value);

                // Account for the fact that you can aim patterns in a straight line
                signedAngleDifference *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));

                double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);

                directionChangeFactor += Math.Max(signedAngleDifference, angleDifference);
            }

            return directionChangeFactor;
        }
    }
}
