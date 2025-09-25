// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
            var osuNextObj = (OsuDifficultyHitObject)current.Next(0);

            if (osuPrevObj.IsNull() || osuNextObj.IsNull())
            {
                return Math.Pow(osuCurrObj.LazyJumpDistance, 1.8) / osuCurrObj.AdjustedDeltaTime * 0.12;
            }

            double difficulty = Math.Pow(osuCurrObj.LazyJumpDistance, 1.7) * 0.15;

            double adjustedDistanceScale = 1.0;

            if (osuCurrObj.Angle != null && osuPrevObj?.Angle != null &&
                Math.Abs(osuCurrObj.DeltaTime - osuPrevObj.DeltaTime) < 25 &&
                Math.Abs(osuNextObj.DeltaTime - osuCurrObj.DeltaTime) < 25)
            {
                double angleDifferenceAdjusted = Math.Sin(directionChange(osuCurrObj, osuPrevObj) / 2) * 180.0;
                double angularVelocity = angleDifferenceAdjusted / (0.1 * osuCurrObj.AdjustedDeltaTime);
                double angularVelocityBonus = Math.Max(0.0, 1.5 * Math.Log10(angularVelocity));

                // ensure that distance is consistent
                var distances = new List<double>();

                for (int i = 0; i < 16; i++)
                {
                    var obj = current.Index > i ? (OsuDifficultyHitObject)current.Previous(i) : null;
                    var objPrev = current.Index > i + 1 ? (OsuDifficultyHitObject)current.Previous(i + 1) : null;

                    if (obj != null && objPrev != null)
                    {
                        if (Math.Abs(obj.DeltaTime - objPrev.DeltaTime) > 25)
                            break;

                        distances.Add(Math.Abs(obj.MinimumJumpDistance - objPrev.MinimumJumpDistance));
                    }
                }

                double averageDistanceDifference = distances.Count > 0 ? distances.Average() : 0;
                double distanceDifferenceScaling = Math.Max(0, 1.0 - averageDistanceDifference / 30.0);
                adjustedDistanceScale = Math.Min(1.0, 0.6 + averageDistanceDifference / 30.0) + angularVelocityBonus * distanceDifferenceScaling;
            }

            difficulty *= adjustedDistanceScale;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty / osuCurrObj.AdjustedDeltaTime * 1.2;
        }

        private static double directionChange(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuPrevObj)
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
