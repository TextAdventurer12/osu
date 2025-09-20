// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (!IsValid(current, 2))
                return 0;

            const int radius = NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 0.5, 1);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 0.5, 1);

            // If the previous notes are stacked, we add the previous note's strainTime since there was no movement since at least 2 notes earlier.
            // https://youtu.be/-yJPIk-YSLI?t=186
            double currTime = osuCurrObj.AdjustedDeltaTime + osuPrevObj.AdjustedDeltaTime * (1 - prevDistanceMultiplier);
            double prevTime = osuPrevObj.AdjustedDeltaTime;

            double baseFactor = 1;
            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj.Angle.Value;

                baseFactor = 1 - 0.3 * Smoothstep(lastAngle, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngle, lastAngle);

                angleBonus = Smootherstep(currAngle, 0, 120) * 0.2;
            }

            // Penalize angle repetition.
            double angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * 0.95 * angleVectorRepetition(osuCurrObj), 2);

            // We reward high bpm more for wider angles, but only when both current and previous distance are over 0.5 radii.
            double baseBpm = 270.0 / (1 + angleBonus * currDistanceMultiplier * prevDistanceMultiplier);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 4.0) - 1);

            return agilityBonus * angleRepetitionNerf * 10;
        }

        private static double angleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        private static double angleVectorRepetition(OsuDifficultyHitObject current)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            int index = 0;
            double notesProcessed = 0;

            while (notesProcessed < note_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                if (loopObj.VectorAngle.IsNotNull() && current.VectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.VectorAngle.Value - loopObj.VectorAngle.Value);
                    constantAngleCount += Math.Cos(8 * Math.Min(Math.PI / 16, angleDifference));
                }

                notesProcessed++;
                index++;
            }

            return Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);
        }
    }
}
