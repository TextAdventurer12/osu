﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
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

        private double hitWindow300, hitWindow100, hitWindow50;
        private double deviation, speedDeviation;

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

            double clockRate = getClockRate(score);
            hitWindow300 = 80 - 6 * osuAttributes.OverallDifficulty;
            hitWindow100 = (140 - 8 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;

            deviation = calculateTotalDeviation(score, osuAttributes);
            speedDeviation = calculateSpeedDeviation(score, osuAttributes);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score);
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
            if (deviation == double.PositiveInfinity)
                return 0.0;

            double aimValue = Math.Pow(5.0 * Math.Max(1.0, attributes.AimDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.5 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                aimValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), effectiveMissCount);

            aimValue *= getComboScalingFactor(attributes);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.1 * (attributes.ApproachRate - 10.33);
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

            // Apply antirake nerf
            double totalAntiRakeMultiplier = calculateTotalRakeNerf(attributes);
            aimValue *= totalAntiRakeMultiplier;

            // Scale the aim value with adjusted deviation
            double adjustedDeviation = deviation * calculateDeviationArAdjust(attributes.ApproachRate);
            aimValue *= SpecialFunctions.Erf(33 / (Math.Sqrt(2) * adjustedDeviation));
            aimValue *= 0.98 + Math.Pow(100.0 / 9, 2) / 2500; // OD 11 SS stays the same.

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == double.PositiveInfinity)
                return 0.0;

            double speedValue = Math.Pow(5.0 * Math.Max(1.0, attributes.SpeedDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.5 * Math.Min(1.0, totalHits / 2000.0) +
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

            // Apply antirake nerf
            double speedAntiRakeMultiplier = calculateSpeedRakeNerf(attributes);
            speedValue *= speedAntiRakeMultiplier;

            double adjustedSpeedDeviation = speedDeviation * calculateDeviationArAdjust(attributes.ApproachRate);
            speedValue *= SpecialFunctions.Erf(22 / (Math.Sqrt(2) * adjustedSpeedDeviation * Math.Max(1, Math.Pow(attributes.SpeedDifficulty / 4.5, 1.2))));
            speedValue *= 0.95 + Math.Pow(100.0 / 9, 2) / 750; // OD 11 SS stays the same.

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double accuracyValue = 120 * Math.Pow(7.5 / deviation, 2);

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight) || deviation == double.PositiveInfinity)
                return 0.0;

            double flashlightValue = Math.Pow(attributes.FlashlightDifficulty, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with adjusted deviation
            double adjustedDeviation = deviation * calculateDeviationArAdjust(attributes.ApproachRate);
            flashlightValue *= SpecialFunctions.Erf(55 / (Math.Sqrt(2) * adjustedDeviation));
            flashlightValue *= 0.98 + Math.Pow(100.0 / 9, 2) / 2500;  // OD 11 SS stays the same.

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

        /// <summary>
        /// Using <see cref="calculateDeviation"/> estimates player's deviation on accuracy objects.
        /// Returns deviation for circles and sliders if score was set with slideracc.
        /// Returns the min between deviation of circles and deviation on circles and sliders (assuming slider hits are 50s), if score was set without slideracc.
        /// </summary>
        private double calculateTotalDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            int accuracyObjectCount = attributes.HitCircleCount;

            // Assume worst case: all mistakes was on accuracy objects
            int relevantCountMiss = Math.Min(countMiss, accuracyObjectCount);
            int relevantCountMeh = Math.Min(countMeh, accuracyObjectCount - relevantCountMiss);
            int relevantCountOk = Math.Min(countOk, accuracyObjectCount - relevantCountMiss - relevantCountMeh);
            int relevantCountGreat = Math.Max(0, accuracyObjectCount - relevantCountMiss - relevantCountMeh - relevantCountOk);

            // Calculate deviation on accuracy objects
            double deviation = calculateDeviation(relevantCountGreat, relevantCountOk, relevantCountMeh, relevantCountMiss);

            // If score was set without slider accuracy - also compute deviation with sliders
            // Assume that all hits was 50s
            int totalCountWithSliders = attributes.HitCircleCount + attributes.SliderCount;
            int missCountWithSliders = Math.Min(totalCountWithSliders, countMiss);
            int hitCountWithSliders = totalCountWithSliders - missCountWithSliders;

            double hitProbabilityWithSliders = hitCountWithSliders / (totalCountWithSliders + 1.0);
            double deviationWithSliders = hitWindow50 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(hitProbabilityWithSliders));

            // Min is needed for edgecase maps with 1 circle and 999 sliders, as deviation on sliders can be lower in this case
            return Math.Min(deviation, deviationWithSliders);
        }

        /// <summary>
        /// Using <see cref="calculateDeviation"/> estimates player's deviation on speed notes, assuming worst-case.
        /// Treats all speed notes as hit circles. This is not good way to do this, but fixing this is impossible under the limitation of current speed pp.
        /// If score was set with slideracc - tries to remove mistaps on sliders from total mistaps.
        /// </summary>
        /// <summary>
        /// Does the same as <see cref="calculateDeviation"/>, but only for notes and inaccuracies that are relevant to speed difficulty.
        /// Treats all difficult speed notes as circles, so this method can sometimes return a lower deviation than <see cref="calculateDeviation"/>.
        /// This is fine though, since this method is only used to scale speed pp.
        /// </summary>
        /// <summary>
        /// Using <see cref="calculateDeviation"/> estimates player's deviation on speed notes, assuming worst-case.
        /// Treats all speed notes as hit circles. This is not good way to do this, but fixing this is impossible under the limitation of current speed pp.
        /// If score was set with slideracc - tries to remove mistaps on sliders from total mistaps.
        /// </summary>
        private double calculateSpeedDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return double.PositiveInfinity;

            // Calculate accuracy assuming the worst case scenario
            double speedNoteCount = attributes.SpeedNoteCount;

            // Assume worst case: all mistakes was on speed notes
            double relevantCountMiss = Math.Min(countMiss, speedNoteCount);
            double relevantCountMeh = Math.Min(countMeh, speedNoteCount - relevantCountMiss);
            double relevantCountOk = Math.Min(countOk, speedNoteCount - relevantCountMiss - relevantCountMeh);
            double relevantCountGreat = Math.Max(0, speedNoteCount - relevantCountMiss - relevantCountMeh - relevantCountOk);

            // Calculate and return deviation on speed notes
            return calculateDeviation(relevantCountGreat, relevantCountOk, relevantCountMeh, relevantCountMiss);
        }

        /// <summary>
        /// Estimates the player's tap deviation based on the OD, given number of 300s, 100s, 50s and misses,
        /// assuming the player's mean hit error is 0. The estimation is consistent in that two SS scores on the same map with the same settings
        /// will always return the same deviation. Misses are ignored because they are usually due to misaiming.
        /// 300s and 100s are assumed to follow a normal distribution, whereas 50s are assumed to follow a uniform distribution.
        /// </summary>
        private double calculateDeviation(double relevantCountGreat, double relevantCountOk, double relevantCountMeh, double relevantCountMiss)
        {
            if (relevantCountGreat + relevantCountOk + relevantCountMeh <= 0)
                return double.PositiveInfinity;

            double objectCount = relevantCountGreat + relevantCountOk + relevantCountMeh + relevantCountMiss;

            //// The probability that a player hits a circle is unknown, but we can estimate it to be
            //// the number of greats on circles divided by the number of circles, and then add one
            //// to the number of circles as a bias correction.
            double n = Math.Max(1, objectCount - relevantCountMiss - relevantCountMeh);
            const double z = 2.32634787404; // 99% critical value for the normal distribution (one-tailed).

            // Proportion of greats hit on circles, ignoring misses and 50s.
            double p = relevantCountGreat / n;

            // We can be 99% confident that p is at least this value.
            double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

            // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
            // Begin with 300s and 100s first. Ignoring 50s, we can be 99% confident that the deviation is not higher than:
            double deviation = hitWindow300 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(pLowerBound));

            double adjustFor100 = Math.Sqrt(2 / Math.PI) * hitWindow100 * Math.Exp(-0.5 * Math.Pow(hitWindow100 / deviation, 2))
                / (deviation * SpecialFunctions.Erf(hitWindow100 / (Math.Sqrt(2) * deviation)));

            deviation *= Math.Sqrt(1 - adjustFor100);

            // Value deviation approach as greatCount approaches 0
            double limitValue = hitWindow100 / Math.Sqrt(3);

            // If precision is not enough to compute true deviation - use limit value
            if (pLowerBound == 0 || adjustFor100 >= 1 || deviation > limitValue)
                deviation = limitValue;

            // Then compute the variance for 50s.
            double mehVariance = (hitWindow50 * hitWindow50 + hitWindow100 * hitWindow50 + hitWindow100 * hitWindow100) / 3;

            // Find the total deviation.
            deviation = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviation, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

            return deviation;
        }

        // Calculates multiplier for speed accounting for rake based on the deviation and speed difficulty
        // https://www.desmos.com/calculator/puc1mzdtfv
        private double calculateSpeedRakeNerf(OsuDifficultyAttributes attributes)
        {
            // Base speed value
            double speedValue = 4 * Math.Pow(attributes.SpeedDifficulty, 3);

            // Starting from this pp amount - penalty will be applied
            double abusePoint = 100 + 260 * Math.Pow(22 / speedDeviation, 5.8);

            if (speedValue <= abusePoint)
                return 1.0;

            // Use log curve to make additional rise in difficulty unimpactful. Rescale values to make curve have correct steepness
            const double scale = 50;
            double adjustedSpeedValue = scale * (Math.Log((speedValue - abusePoint) / scale + 1) + abusePoint / scale);

            double speedRakeNerf = adjustedSpeedValue / speedValue;

            return Math.Min(speedRakeNerf, calculateTotalRakeNerf(attributes));
        }

        // Calculates multiplier for total pp accounting for rake based on the deviation and sliderless aim and speed difficulty
        private double calculateTotalRakeNerf(OsuDifficultyAttributes attributes)
        {
            // Use adjusted deviation to not nerf EZHT aim maps
            double adjustedDeviation = deviation * calculateDeviationArAdjust(attributes.ApproachRate);

            // Base values
            double aimNoSlidersValue = 4 * Math.Pow(attributes.AimDifficulty * attributes.SliderFactor, 3);
            double speedValue = 4 * Math.Pow(attributes.SpeedDifficulty, 3);
            double totalValue = Math.Pow(Math.Pow(aimNoSlidersValue, 1.1) + Math.Pow(speedValue, 1.1), 1 / 1.1);

            // Starting from this pp amount - penalty will be applied
            double abusePoint = 200 + 600 * Math.Pow(22 / adjustedDeviation, 4.2);

            if (totalValue <= abusePoint)
                return 1.0;

            // Use relax penalty after the point to make values grow slower but still noticeably
            double adjustedTotalValue = abusePoint + Math.Pow(0.9, 3) * (totalValue - abusePoint);

            return adjustedTotalValue / totalValue;
        }

        // Bonus for low AR to account for the fact that it's more difficult to get low UR on low AR
        private static double calculateDeviationArAdjust(double AR) => 0.475 + 0.7 / (1.0 + Math.Pow(1.73, 7.9 - AR));

        private static double getClockRate(ScoreInfo score)
        {
            var track = new TrackVirtual(1);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            return track.Rate;
        }
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

        private int totalHits => countGreat + countOk + countMeh + countMiss;

        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
