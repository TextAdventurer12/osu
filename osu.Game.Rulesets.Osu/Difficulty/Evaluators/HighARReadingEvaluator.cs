// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class HighARReadingEvaluator
    {
        private const double preempt_balancing_factor = 160000;

        public static double EvaluateDifficultyOf(DifficultyHitObject current, double preempt)
        {
            if (current.BaseObject is Spinner || current.Index == 0)
                return 0;

            var currObj = (OsuDifficultyHitObject)current;
            double constantAngleNerfFactor = getConstantAngleNerfFactor(currObj);
            double velocity = Math.Max(1, currObj.MinimumJumpDistance / currObj.StrainTime); // Only allow velocity to buff

            double preemptDifficulty = 0.0;

            // Arbitrary curve for the base value preempt difficulty should have as approach rate increases.
            // https://www.desmos.com/calculator/qmqxuukqqe
            preemptDifficulty += preempt > 475 ? 0 : Math.Pow(475 - preempt, 2.4) / preempt_balancing_factor;

            preemptDifficulty *= constantAngleNerfFactor * velocity;

            return preemptDifficulty;
        }

        // Returns a factor of how often the current object's angle has been repeated in a certain time frame.
        // It does this by checking the difference in angle between current and past objects and sums them based on a range of similarity.
        // https://www.desmos.com/calculator/cjlvp8pjah
        private static double getConstantAngleNerfFactor(OsuDifficultyHitObject current)
        {
            const double time_limit = 2000; // 2 seconds
            const double time_limit_low = 200;

            double constantAngleCount = 0;
            int index = 0;
            double currentTimeGap = 0;

            while (currentTimeGap < time_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                // Account less for objects that are close to the time limit.
                double longIntervalFactor = Math.Clamp(1 - (loopObj.StrainTime - time_limit_low) / (time_limit - time_limit_low), 0, 1);

                if (loopObj.Angle.IsNotNull() && current.Angle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.Angle.Value - loopObj.Angle.Value);
                    constantAngleCount += Math.Cos(3 * Math.Min(Math.PI / 6, angleDifference)) * longIntervalFactor;
                }

                currentTimeGap = current.StartTime - loopObj.StartTime;
                index++;
            }

            return Math.Min(1, 2 / constantAngleCount);
        }
    }
}
