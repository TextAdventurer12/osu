// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Aggregation;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        private class StaminaObject
        {
            public double clock_time;
            public double difficulty;
            public double deltaTime;

            private double ClockAdjustment(double delta)
            {
                // Below this bpm 1/4 we start to increase delta time to better account for stamina recovery
                const double tau = 90;
                // Delta  time multiplier for low bpms approaches tau_units ^ q. Increasing q will nerf anything below tau.
                const double q = 1.25;
                // Changes how sharply the multiplier scales around tau. Increasing m will strengthen the effect below tau and weaken it above
                const double m = 5;
                double tau_units = DiffUtils.Pow(tau / DiffUtils.MillisecondsToBPM(delta), m);
                return delta * DiffUtils.Pow(1 + tau_units, q / m) / 1000;
            }

            public StaminaObject(double difficulty, double last_clock, double deltaTime)
            {
                this.difficulty = difficulty;
                this.deltaTime = deltaTime;
                this.clock_time = last_clock + ClockAdjustment(deltaTime);
            }
        }
        private readonly List<double> sliderStrains = new List<double>();
		private readonly List<StaminaObject> staminaMemory = new List<StaminaObject>();

        private double harmonicWeightSum;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

		private const double strain_constant = 1.4;

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            const double skill_multiplier = 1.8;

            if (Mods.Any(m => m is OsuModRelax))
                return 0;

			double currentDifficulty = calculateAdjustedDifficulty(current);

            double clock_time = 0;
            if (staminaMemory.Count != 0)
                clock_time = staminaMemory.Last().clock_time;

            StaminaObject currStaminaObj = new StaminaObject(currentDifficulty, clock_time, current.DeltaTime);
            staminaMemory.Add(currStaminaObj);

			double currentStrain = 0;
            foreach (StaminaObject obj in staminaMemory)
                currentStrain += obj.difficulty / (1 + strain_constant * (currStaminaObj.clock_time - obj.clock_time));

			currentStrain *= skill_multiplier;

            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModAutopilot))
                difficulty *= 0.5;

            return difficulty;
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, harmonicWeightSum) = HarmonicSeries.Aggregate(ObjectDifficulties, harmonicScale: 20);

            return difficulty;
        }

        public double RelevantObjectCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => DiffUtils.Logistic(strain / maxStrain, 0.5, 12.0));
        }

        public virtual double CountTopWeightedObjectDifficulties(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top difficulty be if all object difficulties were identical

            if (consistentTopObject == 0)
                return 0;

            return ObjectDifficulties.Sum(d => DiffUtils.Logistic(d / consistentTopObject, 0.88, 10, 1.1));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top note be if all note values were identical

            if (consistentTopObject == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopObject, 0.88, 10, 1.1));
        }
    }
}
