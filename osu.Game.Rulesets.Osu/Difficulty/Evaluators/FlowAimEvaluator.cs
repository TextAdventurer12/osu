// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
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
                double angularVelocityBonus = Math.Max(0.0, 0.4 * Math.Log10(angularVelocity));

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
                double distanceDifferenceScaling = Math.Max(0, 1.0 - averageDistanceDifference / 50.0);

                adjustedDistanceScale = Math.Min(1.0, 0.6 + averageDistanceDifference / 30.0) + angularVelocityBonus * distanceDifferenceScaling;
            }

            // Base snap difficulty is velocity.
            double difficulty = Math.Pow(osuCurrObj.LazyJumpDistance, 2) * 0.01 * adjustedDistanceScale / osuCurrObj.StrainTime;

            return difficulty * osuCurrObj.SmallCircleBonus;
        }
    }
}
