// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        const double impulse_weight = 1;
        const double force_weight = 1;
        const double velocity_weight = 1;
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double f_c = centripedal_force(current);
            double f_cp = centripedal_force(current.Previous(0));
            double d_f = Math.Abs(f_c - f_cp);
            double impulse = d_f / osuCurrObj.StrainTime;
            double velocity = osuLastObj.LazyJumpDistance / osuLastObj.StrainTime;

            return velocity * velocity_weight + f_c * force_weight + impulse * impulse_weight;
        }
        public static double centripedal_force(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);

            if (osuLastObj.Angle == Math.PI)
                return 0;

            double curr_lastlast_d = (osuCurrObj.BaseObject.StackedPosition - osuLastLastObj.BaseObject.StackedPosition).Length;
            double flow_radius = curr_lastlast_d / (2 * Math.Sin(osuLastObj.Angle));

            double v = osuLastObj.LazyJumpDistance / osuLastObj.StrainTime;
            return v * v / flow_radius;
        }
    }
}
