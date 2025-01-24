// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Rhythm : OsuSkill
    {
        public Rhythm(Mod[] mods)
            : base(mods)
        {
        }

        protected override double K => 2;
        protected override double Multiplier => 1;
        private readonly List<(List<OsuDifficultyHitObject> objects, double time)> islands = new List<(List<OsuDifficultyHitObject>, double)>();
        private const double time_multiplier = 1;
        private const double length_multiplier = 1;

        protected override void CalculateDifficulties()
        {
            islands[^1] = (islands[^1].objects, islands[^1].objects.Average(obj => obj.StrainTime));
            List<(List<OsuDifficultyHitObject> objects, double time)> pastIslands = new List<(List<OsuDifficultyHitObject>, double)>();

            foreach (var island in islands)
            {
                double difficulty = 0;
                List<double> timeRatios = new List<double>();
                List<double> lengthRatios = new List<double>();

                foreach (var pastIsland in pastIslands)
                {
                    timeRatios.Add(pastIsland.time / island.time);
                    if (island.objects != null) lengthRatios.Add((double)pastIsland.objects.Count / island.objects.Count);
                }

                double timeRatioDifficulty = 0;

                for (int i = 0; i < timeRatios.Count; i++)
                {
                    // sum ratio of previous difficulties using geometric decay
                    timeRatioDifficulty += RhythmComplexityEvaluator.EvaluateTimeRatioDifficulty(timeRatios[i]) * Math.Pow(0.9, i);
                }

                double lengthRatioDifficulty = 0;

                for (int i = 0; i < lengthRatios.Count; i++)
                {
                    lengthRatioDifficulty = RhythmComplexityEvaluator.EvaluateLengthRatioDifficulty(lengthRatios[i]) * Math.Pow(0.9, i);
                }

                difficulty = timeRatioDifficulty * time_multiplier + lengthRatioDifficulty * length_multiplier;

                Difficulties.Add(difficulty);
                pastIslands.Insert(0, island);
            }
        }

        /// <summary>
        /// we don't do any difficulty calculation inside of Process, instead simply storing all of the 'islands' (groups of similar rhythm notes) to be used during CalculateDifficulties
        /// </summary>
        /// <param name="current"></param>
        public override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            if (osuCurrObj.Index == 0 || !DifficultyCalculationUtils.SimilarRhythm(osuCurrObj, (OsuDifficultyHitObject)osuCurrObj.Previous(0)))
            {
                islands[^1] = (islands[^1].objects, islands[^1].objects.Average(obj => obj.StrainTime));
                islands.Add((new List<OsuDifficultyHitObject>(), 0));
            }

            islands.Last().objects.Add(osuCurrObj);
        }
    }
}
