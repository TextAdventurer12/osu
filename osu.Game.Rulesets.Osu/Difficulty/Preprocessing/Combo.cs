// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class Combo
    {
        public List<double> strains;
        public List<OsuDifficultyHitObject> objects;
        public Combo(List<double> strains, List<OsuDifficultyHitObject> objects)
        {
            this.strains = new List<double>(strains);
            this.objects = objects;
        }

        public readonly List<double> strainPeaks = new List<double>();
        private void AssignStrainPeaks()
        {
            strainPeaks.Clear();
            double currentPeak = 0;
            double strainStartTime = -400;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].StartTime - strainStartTime > 400)
                {
                    strainPeaks.Add(currentPeak);
                    currentPeak = 0;
                    strainStartTime = objects[i].StartTime;
                }
                currentPeak = Math.Max(strains[i], currentPeak);
            }
        }
        public double DifficultyValue()
        {
            AssignStrainPeaks();
            if (strains.Count == 0)
                return 0;
            double difficulty = 0;
            double weight = 1;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var peaks = strainPeaks.Where(p => p > 0);

            List<double> secStrains = peaks.OrderDescending().ToList();

            // We are reducing the highest strains first to account for extreme difficulty spikes
            for (int i = 0; i < Math.Min(secStrains.Count, 10); i++)
            {
                double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((float)i / 10, 0, 1)));
                secStrains[i] *= Interpolation.Lerp(0.75, 1.0, scale);
            }

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in secStrains.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            return Math.Sqrt(difficulty * 1.06) * 0.0675;
        }
    }
}