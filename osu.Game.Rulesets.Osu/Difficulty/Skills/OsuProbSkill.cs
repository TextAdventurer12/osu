// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuProbSkill : Skill
    {
        protected OsuProbSkill(Mod[] mods, double radius)
            : base(mods)
        {
            this.radius = radius;
        }

        /// The skill level returned from this class will have FcProbability chance of hitting every note correctly.
        /// A higher value rewards short, high difficulty sections, whereas a lower value rewards consistent, lower difficulty.
        protected abstract double FcProbability { get; }

        protected abstract double skillMultiplier { get; }
        protected double radius { get; private set;}

        public readonly List<double> difficulties = new List<double>();

        /// <summary>
        /// Returns the devation expected of a player of skill 1 at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double DeviationAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(DeviationAt(current));
        }

        protected double SuccessProbability(double skill, double difficulty)
        {
            if (skill <= 0) return 0;
            if (difficulty <= 0) return 1;

            double deviation = difficulty / skill;

            return SpecialFunctions.Erf(radius / (Math.Sqrt(2) * deviation));
        }

        private double difficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - FcProbability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return bins.Aggregate(1.0, (current, bin) => current * Math.Pow(SuccessProbability(s, bin.Difficulty), bin.Count));
            }
        }

        private double difficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - FcProbability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return difficulties.Aggregate<double, double>(1, (current, d) => current * SuccessProbability(s, d));
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0)
                return 0;

            return difficulties.Count < 64 ? difficultyValueExact() : difficultyValueBinned();
        }

        /// <summary>
        /// Find the lowest errorcount that a player with the provided <paramref name="skill"/> would have a 2% chance of achieving.
        /// </summary>
        public double GetErrorCountAtSkill(double skill)
        {
            double maxDiff = difficulties.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return difficulties.Count;

            PoissonBinomial poiBin;

            if (difficulties.Count > 64)
            {
                var bins = Bin.CreateBins(difficulties);
                poiBin = new PoissonBinomial(bins, skill, SuccessProbability);
            }
            else
            {
                poiBin = new PoissonBinomial(difficulties, skill, SuccessProbability);
            }

            return Math.Max(0, RootFinding.FindRootExpand(x => poiBin.CDF(x) - FcProbability, -50, 1000, accuracy: 1e-4));
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the error counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for ax^4+bx^3+cx^2. The 4th coefficient for dx^1 can be deduced from the first 3 in the performance calculator.</returns>
        public ExpPolynomial GetErrorCountPolynomial()
        {
            const int count = 21;
            const double penalty_per_errorcount = 1.0 / (count - 1);

            double fcSkill = DifficultyValue();

            double[] errorcounts = new double[count];

            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    errorcounts[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill - fcSkill * penalty_per_errorcount * i;

                errorcounts[i] = GetErrorCountAtSkill(penalizedSkill);
            }

            ExpPolynomial polynomial = new ExpPolynomial();

            polynomial.Compute(errorcounts, 3);

            return polynomial;
        }
    }
}
