// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmComplexityEvaluator
    {
        /// <summary>
        /// I think taiko cooked something with this?
        /// basically have some kind of sum of sines that corresponds with the difficulty of certain ratios
        /// </summary>
        /// <param name="timeRatio"></param>
        /// <returns></returns>
        public static double EvaluateTimeRatioDifficulty(double timeRatio)
        {
            return timeRatio;
        }

        /// <summary>
        /// same as above
        /// </summary>
        /// <param name="lengthRatio"></param>
        /// <returns></returns>
        public static double EvaluateLengthRatioDifficulty(double lengthRatio)
        {
            return lengthRatio;
        }
    }
}
