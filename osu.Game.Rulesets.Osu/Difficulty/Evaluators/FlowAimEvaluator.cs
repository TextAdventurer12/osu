// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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

            double adjustedDistanceScale = 1.0;

            if (osuCurrObj.Angle.HasValue &&
                osuPrevObj?.Angle != null &&
                Math.Abs(osuCurrObj.DeltaTime - osuPrevObj.DeltaTime) < 25)
            {
                double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);
                double angleDifferenceAdjusted = Math.Sin(angleDifference / 2) * 180.0;
                double angularVelocity = angleDifferenceAdjusted / (0.1 * osuCurrObj.StrainTime);
                double angularVelocityBonus = Math.Max(0.0, 10 * Math.Log10(angularVelocity));

                // ensure that distance is consistent
                var distances = new List<double>();

                for (int i = 0; i < 16; i++)
                {
                    if (osuPrevObj == null) continue;

                    if (Math.Abs(osuCurrObj.DeltaTime - osuPrevObj.DeltaTime) > 25)
                        break;

                    distances.Add(Math.Abs(osuCurrObj.MinimumJumpDistance - osuPrevObj.MinimumJumpDistance));
                }

                double averageDistanceDifference = distances.Count > 0 ? distances.Average() : 0;
                double distanceDifferenceScaling = Math.Max(0, 1.0 - averageDistanceDifference / 30.0);

                adjustedDistanceScale = Math.Min(1.0, 0.6 + averageDistanceDifference / 30.0) + angularVelocityBonus * distanceDifferenceScaling;
            }

            double simulatedDistance = adjustFlowDistance(osuCurrObj);

            // Base snap difficulty is velocity.
            double difficulty = simulatedDistance * adjustedDistanceScale / osuCurrObj.StrainTime;

            return difficulty * osuCurrObj.SmallCircleBonus;
        }

        /// <summary>
        /// Approximate the amount of unnecessary distance the cursor will travel in an arc attempting to flow between notes
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private static double adjustFlowDistance(DifficultyHitObject current)
        {
            var osuCurr = (OsuDifficultyHitObject)current;
            var osuPrev = (OsuDifficultyHitObject)current.Previous(0);

            // If angle is missing, it's just distance
            if (!osuCurr.Angle.HasValue)
                return osuCurr.LazyJumpDistance;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            double angle = osuCurr.Angle.Value;
            double distanceTravelled = osuCurr.LazyJumpDistance;

            double maxBonusAngle = double.DegreesToRadians(140);

            if (angle >= maxBonusAngle)
                return distanceTravelled;

            //extra distance is a function of previous velocity, your arc will be less tight if you're coming in hot
            double previousVelocity = osuPrev.LazyJumpDistance / osuPrev.StrainTime;
            var osuLastLastObj = (OsuDifficultyHitObject)osuPrev.Previous(0);

            if (osuPrev.Previous(0).BaseObject is Slider)
            {
                double travelVelocity = osuLastLastObj.TravelDistance / osuLastLastObj.TravelTime;
                double movementVelocity = osuPrev.MinimumJumpDistance / osuPrev.MinimumJumpTime;

                previousVelocity = Math.Max(previousVelocity, movementVelocity + travelVelocity);
            }

            //the sharper the angle, the more inefficient the real path will be
            double angleScale = 1.0 - DifficultyCalculationUtils.Smootherstep(angle, 0, maxBonusAngle);

            //nerf cheesable distances where the angle isn't indicative of the path the cursor takes between notes
            angleScale *= DifficultyCalculationUtils.Smootherstep(osuCurr.LazyJumpDistance, radius * 2, radius * 2.5);

            double velocityBonus = 1 + previousVelocity * angleScale * 0.75;

            return Math.Pow(distanceTravelled, velocityBonus);
        }
    }
}
