// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// A skill which keeps track of strain difficulty while also not dividing difficulty by time
    /// </summary>
    public class ProportionateStrainSkill : OsuSkill
    {
        public Aim(Mod[] mods)
            : base(mods)
        {
        }

        private readonly List<(double difficulty, double deltaTime)> previousStrains = new List<(double, double)>();

        private double currentStrain;
        protected double currentDifficulty;

        private double strainDecayBase => 0.15;
        private double strainIncreaseRate => 10;
        private double strainDecreaseRate => 3;
        private double strainInfluence => 3 / 1;

        protected double StrainValueAt(OsuDifficultyHitObject current)
        {
            double priorDifficulty = highestPreviousStrain(current.DeltaTime);

            currentStrain = getStrainValueOf(currentDifficulty, priorDifficulty);
            previousStrains.Add((currentStrain, current.DeltaTime));

            return currentDifficulty + currentStrain * strainInfluence;
        }

        private double getStrainValueOf(double currentDifficulty, double priorDifficulty) => currentDifficulty > priorDifficulty
            ? (priorDifficulty * strainIncreaseRate + currentDifficulty) / (strainIncreaseRate + 1)
            : (priorDifficulty * strainDecreaseRate + currentDifficulty) / (strainDecreaseRate + 1);

        private double highestPreviousStrain(double time)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltaTime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 400, 7));

            for (int i = 0; i < previousStrains.Count; i++)
            {
                if (cumulativeDeltaTime > 1200)
                {
                    previousStrains.RemoveRange(0, i);
                    break;
                }

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, previousStrains[^(i + 1)].difficulty * timeDecay(cumulativeDeltaTime));

                cumulativeDeltaTime += previousStrains[^i].deltaTime;
            }

            return hardestPreviousDifficulty;
        }
    }
}