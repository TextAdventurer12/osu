// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Osu.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class Rhythm : OsuSkill
    {
        protected override double k => 2;
        protected override double multiplier => 1;
        private List<(List<OsuDifficultyHitObject> objects, double time)> islands = new List<(List<OsuDifficultyHitObject>, double)>();
        private const double time_multiplier = 1;
        private const double length_multiplier = 1;
        protected override void CalculateDifficulties()
        {
            islands[^1].time = islands[^1].Average(obj => obj.StrainTime);
            List<(List<OsuDifficultyHitObject> objects, double time)> pastIslands = new List<(List<OsuDifficultyHitObject>, double)>();
            foreach (var island in islands)
            {
                double difficulty = 0;
                List<double> timeRatios = new List<double>();
                List<double> lengthRatios = new List<double>();
                foreach (var pastIsland in pastIslands.Reverse())
                {
                    timeRatios.Add(pastIsland.time / island.time);
                    lengthRatios.Add(pastIsland.objects.Count / island.objects.Count);
                }

                double timeRatioDifficulty = 0;
                for (int i = 0; i < timeRatios.Count(); i++)
                {
                    // sum ratio of previous difficulties using geometric decay
                    timeRatioDifficulty += RhythmComplexityEvaluator.EvaluateTimeRatioDifficulty(timeRatios[i]) * Math.Pow(0.9, i);
                }
                double lengthRatioDifficulty = 0;
                for (int i = 0; i < lengthRatios.Count(); i++)
                {
                    lengthRatioDifficulty == RhythmComplexityEvaluator.EvaluateLengthRatioDifficulty(lengthRatios[i]) * Math.Pow(0.9, i);
                }

                difficulty = timeRatioDifficulty * time_multiplier + lengthRatioDifficulty * length_multiplier;

                difficulties.Add(difficulty);
                pastIslands.Add(island);
            }
        }
        /// <summary>
        /// we don't do any difficulty calculation inside of Process, instead simply storing all of the 'islands' (groups of similar rhythm notes) to be used during CalculateDifficulties
        /// </summary>
        /// <param name="current"></param>
        protected override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            if (osuCurrObj.Index == 0 || !DifficultyCalculationUtils.SimilarRhythm(osuCurrObj, (OsuDifficultyHitObject)osuCurrObj.Previous(0)))
            {
                islands[^1].time = islands[^1].Average(obj => obj.StrainTime);
                islands.Add((new List<OsuDifficultyHitObject>(), 0));
            }
            islands.Last().Add(current);
        }
    }
}