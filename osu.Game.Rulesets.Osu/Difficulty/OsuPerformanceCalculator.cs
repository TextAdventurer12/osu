﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.RootFinding;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.14; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double effectiveMissCount;
        private double deviation;
        private double speedDeviation;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            effectiveMissCount = calculateEffectiveMissCount(osuAttributes);
            deviation = calculateDeviation(score, osuAttributes);
            speedDeviation = calculateSpeedDeviation(score, osuAttributes);

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/bc9eybdthb
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 1.8) : 1.0);
                double mehMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1) +
                    Math.Pow(flashlightValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                Deviation = deviation,
                SpeedDeviation = speedDeviation,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double aimValue = Math.Pow(5.0 * Math.Max(1.0, attributes.AimDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                aimValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), effectiveMissCount);

            aimValue *= getComboScalingFactor(attributes);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);
            else if (attributes.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - attributes.ApproachRate);

            if (score.Mods.Any(h => h is OsuModRelax))
                approachRateFactor = 0.0;

            aimValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // We assume 15% of sliders in a map are difficult since there's no way to tell from the performance calculator.
            double estimateDifficultSliders = attributes.SliderCount * 0.15;

            if (attributes.SliderCount > 0)
            {
                double estimateSliderEndsDropped = Math.Clamp(Math.Min(countOk + countMeh + countMiss, attributes.MaxCombo - scoreMaxCombo), 0, estimateDifficultSliders);
                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateSliderEndsDropped / estimateDifficultSliders, 3) + attributes.SliderFactor;
                aimValue *= sliderNerfFactor;
            }

            // Scale aim value with deviation
            aimValue *= 4169 / 4050.0 * SpecialFunctions.Erf(50 / (Math.Sqrt(2) * deviation));

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double speedValue = Math.Pow(5.0 * Math.Max(1.0, attributes.SpeedDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                speedValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            speedValue *= getComboScalingFactor(attributes);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);

            speedValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantAccuracy = attributes.SpeedNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // Scale the speed value with accuracy and OD.
            speedValue *= (0.95 + Math.Pow(attributes.OverallDifficulty, 2) / 750) * Math.Pow((accuracy + relevantAccuracy) / 2.0, (14.5 - Math.Max(attributes.OverallDifficulty, 8)) / 2);

            // Scale the speed value with # of 50s to punish doubletapping.
            speedValue *= Math.Pow(0.99, countMeh < totalHits / 500.0 ? 0 : countMeh - totalHits / 500.0);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double accuracyValue = 800 * Math.Exp(-deviation / 4);

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            accuracyValue *= 1 - (double)countMiss / totalHits;

            return accuracyValue;
        }

        private double calculateDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            var track = new TrackVirtual(10000);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double hitWindow100 = (140 - 8 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;

            double root2 = Math.Sqrt(2);

            int greatCountOnCircles = Math.Max(0, attributes.HitCircleCount - countOk - countMeh - countMiss);
            int okCountOnCircles = Math.Min(countOk, attributes.HitCircleCount) + 1; // Add one 100 to process SS scores.
            int mehCountOnCircles = Math.Min(countMeh, attributes.HitCircleCount);
            int slidersHit = Math.Max(0, attributes.SliderCount - countMiss);

            // Derivative of erf(x)
            double erfPrime(double x) => 2 / Math.Sqrt(Math.PI) * Math.Exp(-x * x);

            // Let f(x) = erf(x). To find the deviation, we have to maximize the log-likelihood function,
            // which is the same as finding the zero of the derivative of the log-likelihood function.
            double logLikelihoodGradient(double u)
            {
                double t1 = -hitWindow50 * slidersHit * erfPrime(hitWindow50 / (root2 * u)) / SpecialFunctions.Erf(hitWindow50 / (root2 * u));
                double t2 = -hitWindow300 * greatCountOnCircles * erfPrime(hitWindow300 / (root2 * u)) / SpecialFunctions.Erf(hitWindow300 / (root2 * u));
                double t3 = mehCountOnCircles * (-hitWindow100 * erfPrime(hitWindow100 / (root2 * u)) + hitWindow50 * erfPrime(hitWindow50 / (root2 * u))) / (SpecialFunctions.Erfc(hitWindow50 / (root2 * u)) - SpecialFunctions.Erfc(hitWindow100 / (root2 * u)));
                double t4 = okCountOnCircles * (-hitWindow100 * erfPrime(hitWindow100 / (root2 * u)) + hitWindow300 * erfPrime(hitWindow300 / (root2 * u))) / (SpecialFunctions.Erfc(hitWindow300 / (root2 * u)) - SpecialFunctions.Erfc(hitWindow100 / (root2 * u)));
                return (t1 + t2 + t3 + t4) / (root2 * u * u);
            }

            return Brent.FindRootExpand(logLikelihoodGradient, 4, 20, 1e-6, expandFactor: 2);
        }

        private double calculateSpeedDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            var track = new TrackVirtual(10000);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double hitWindow100 = (140 - 8 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double root2 = Math.Sqrt(2);

            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat)) + 1;
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));

            // Derivative of erf(x)
            double erfPrime(double x) => 2 / Math.Sqrt(Math.PI) * Math.Exp(-x * x);

            // Let f(x) = erf(x). To find the deviation, we have to maximize the log-likelihood function,
            // which is the same as finding the zero of the derivative of the log-likelihood function.
            double logLikelihoodGradient(double u)
            {
                double t1 = -hitWindow300 * relevantCountGreat * erfPrime(hitWindow300 / (root2 * u)) / SpecialFunctions.Erf(hitWindow300 / (root2 * u));
                double t2 = relevantCountMeh * (-hitWindow100 * erfPrime(hitWindow100 / (root2 * u)) + hitWindow50 * erfPrime(hitWindow50 / (root2 * u))) / (SpecialFunctions.Erfc(hitWindow50 / (root2 * u)) - SpecialFunctions.Erfc(hitWindow100 / (root2 * u)));
                double t3 = relevantCountOk * (-hitWindow100 * erfPrime(hitWindow100 / (root2 * u)) + hitWindow300 * erfPrime(hitWindow300 / (root2 * u))) / (SpecialFunctions.Erfc(hitWindow300 / (root2 * u)) - SpecialFunctions.Erfc(hitWindow100 / (root2 * u)));
                return (t1 + t2 + t3) / (root2 * u * u);
            }

            return Brent.FindRootExpand(logLikelihoodGradient, 4, 20, 1e-6, expandFactor: 2);
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double flashlightValue = Math.Pow(attributes.FlashlightDifficulty, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + accuracy / 2.0;
            // It is important to also consider accuracy difficulty when doing that.
            flashlightValue *= 0.98 + Math.Pow(attributes.OverallDifficulty, 2) / 2500;

            return flashlightValue;
        }

        private double calculateEffectiveMissCount(OsuDifficultyAttributes attributes)
        {
            // Guess the number of misses + slider breaks from combo
            double comboBasedMissCount = 0.0;

            if (attributes.SliderCount > 0)
            {
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;
                if (scoreMaxCombo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);
            }

            // Clamp miss count to maximum amount of possible breaks
            comboBasedMissCount = Math.Min(comboBasedMissCount, countOk + countMeh + countMiss);

            return Math.Max(countMiss, comboBasedMissCount);
        }

        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);
        private int totalHits => countGreat + countOk + countMeh + countMiss;
    }
}
