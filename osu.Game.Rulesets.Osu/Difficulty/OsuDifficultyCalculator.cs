// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double star_rating_multiplier = 0.0265;

        public override int Version => 20250306;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public static double CalculateRateAdjustedApproachRate(double approachRate, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        public static double CalculateRateAdjustedOverallDifficulty(double overallDifficulty, double clockRate)
        {
            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(overallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;

            return (79.5 - hitWindowGreat) / 6;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            var snapAim = skills.OfType<SnapAim>().Single(a => a.IncludeSliders);
            var snapAimWithoutSliders = skills.OfType<SnapAim>().Single(a => !a.IncludeSliders);
            var flowAim = skills.OfType<FlowAim>().Single(a => a.IncludeSliders);
            var flowAimWithoutSliders = skills.OfType<FlowAim>().Single(a => !a.IncludeSliders);
            var speed = skills.OfType<Speed>().Single();
            var flashlight = skills.OfType<Flashlight>().SingleOrDefault();

            double speedNotes = speed.RelevantNoteCount();

            double snapAimDifficultStrainCount = snapAim.CountTopWeightedStrains();
            double flowAimDifficultStrainCount = flowAim.CountTopWeightedStrains();
            double speedDifficultStrainCount   = speed  .CountTopWeightedStrains();

            double snapAimNoSlidersTopWeightedSliderCount = snapAimWithoutSliders.CountTopWeightedSliders();
            double snapAimNoSlidersDifficultStrainCount   = snapAimWithoutSliders.CountTopWeightedStrains();
            double flowAimNoSlidersTopWeightedSliderCount = flowAimWithoutSliders.CountTopWeightedSliders();
            double flowAimNoSlidersDifficultStrainCount   = flowAimWithoutSliders.CountTopWeightedStrains();

            double snapAimTopWeightedSliderFactor = snapAimNoSlidersTopWeightedSliderCount / Math.Max(1, snapAimNoSlidersDifficultStrainCount - snapAimNoSlidersTopWeightedSliderCount);
            double flowAimTopWeightedSliderFactor = flowAimNoSlidersTopWeightedSliderCount / Math.Max(1, flowAimNoSlidersDifficultStrainCount - flowAimNoSlidersTopWeightedSliderCount);


            double speedTopWeightedSliderCount = speed.CountTopWeightedSliders();
            double speedTopWeightedSliderFactor = speedTopWeightedSliderCount / Math.Max(1, speedDifficultStrainCount - speedTopWeightedSliderCount);

            double difficultSliders = snapAim.GetDifficultSliders();

            double approachRate = CalculateRateAdjustedApproachRate(beatmap.Difficulty.ApproachRate, clockRate);
            double overallDifficulty = CalculateRateAdjustedOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate);

            int hitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            int totalHits = beatmap.HitObjects.Count;

            double snapAimDifficultyValue = snapAim.DifficultyValue();
            double snapAimNoSlidersDifficultyValue = snapAimWithoutSliders.DifficultyValue();
            double flowAimDifficultyValue = flowAim.DifficultyValue();
            double flowAimNoSlidersDifficultyValue = flowAimWithoutSliders.DifficultyValue();
            double speedDifficultyValue = speed.DifficultyValue();

            double mechanicalDifficultyRating = calculateMechanicalDifficultyRating(snapAimDifficultyValue, flowAimDifficultyValue, speedDifficultyValue);
            double sliderFactor = snapAimDifficultyValue > 0 ? OsuRatingCalculator.CalculateDifficultyRating(snapAimNoSlidersDifficultyValue) / OsuRatingCalculator.CalculateDifficultyRating(snapAimDifficultyValue) : 1;

            var osuRatingCalculator = new OsuRatingCalculator(mods, totalHits, approachRate, overallDifficulty, mechanicalDifficultyRating, sliderFactor);

            double snapAimRating = osuRatingCalculator.ComputeSnapAimRating(snapAimDifficultyValue);
            double flowAimRating = osuRatingCalculator.ComputeFlowAimRating(flowAimDifficultyValue);
            double speedRating = osuRatingCalculator.ComputeSpeedRating(speedDifficultyValue);

            double flashlightRating = 0.0;

            if (flashlight is not null)
                flashlightRating = osuRatingCalculator.ComputeFlashlightRating(flashlight.DifficultyValue());

            double sliderNestedScorePerObject = LegacyScoreUtils.CalculateNestedScorePerObject(beatmap, totalHits);
            double legacyScoreBaseMultiplier = LegacyScoreUtils.CalculateDifficultyPeppyStars(beatmap);

            var simulator = new OsuLegacyScoreSimulator();
            var scoreAttributes = simulator.Simulate(WorkingBeatmap, beatmap);

            double baseSnapAimPerformance = OsuStrainSkill.DifficultyToPerformance(snapAimRating);
            double baseFlowAimPerformance = OsuStrainSkill.DifficultyToPerformance(flowAimRating);
            double baseSpeedPerformance = OsuStrainSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);

            double baseAimPerformance = Math.Pow(Math.Pow(baseSnapAimPerformance, 1.1) + Math.Pow(baseFlowAimPerformance, 1.1), 1 / 1.1);

            double basePerformance =
                Math.Pow(
                    Math.Pow(baseAimPerformance, 1.1) +
                    Math.Pow(baseSpeedPerformance, 1.1) +
                    Math.Pow(baseFlashlightPerformance, 1.1), 1.0 / 1.1
                );

            double starRating = calculateStarRating(basePerformance);

            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                SnapAimDifficulty = snapAimRating,
                FlowAimDifficulty = flowAimRating,
                DifficultSliderCount = difficultSliders,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                SliderFactor = sliderFactor,
                SnapAimDifficultStrainCount = snapAimDifficultStrainCount,
                FlowAimDifficultStrainCount = flowAimDifficultStrainCount,
                SpeedDifficultStrainCount = speedDifficultStrainCount,
                SnapAimTopWeightedSliderFactor = snapAimTopWeightedSliderFactor,
                FlowAimTopWeightedSliderFactor = flowAimTopWeightedSliderFactor,
                SpeedTopWeightedSliderFactor = speedTopWeightedSliderFactor,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCircleCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
                NestedScorePerObject = sliderNestedScorePerObject,
                LegacyScoreBaseMultiplier = legacyScoreBaseMultiplier,
                MaximumLegacyComboScore = scoreAttributes.ComboScore
            };

            return attributes;
        }

        private double calculateMechanicalDifficultyRating(double snapAimDifficultyValue, double flowAimDifficultyValue, double speedDifficultyValue)
        {
            double snapAimValue = OsuStrainSkill.DifficultyToPerformance(OsuRatingCalculator.CalculateDifficultyRating(snapAimDifficultyValue));
            double flowAimValue = OsuStrainSkill.DifficultyToPerformance(OsuRatingCalculator.CalculateDifficultyRating(flowAimDifficultyValue));
            double speedValue = OsuStrainSkill.DifficultyToPerformance(OsuRatingCalculator.CalculateDifficultyRating(speedDifficultyValue));

            // BALANCING POINT: THIS MAKES snap/speed and flow/speed hybrid advantaged to snap/flow hybrid
            double aimValue = Math.Pow(Math.Pow(snapAimValue, 1.1) + Math.Pow(flowAimValue, 1.1), 1 / 1.1);
            double totalValue = Math.Pow(Math.Pow(aimValue, 1.1) + Math.Pow(speedValue, 1.1), 1 / 1.1);

            return calculateStarRating(totalValue);
        }

        private double calculateStarRating(double basePerformance)
        {
            if (basePerformance <= 0.00001)
                return 0;

            return Math.Cbrt(OsuPerformanceCalculator.PERFORMANCE_BASE_MULTIPLIER) * star_rating_multiplier * (Math.Cbrt(100000 / Math.Pow(2, 1 / 1.1) * basePerformance) + 4);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], clockRate, objects, objects.Count));
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            var skills = new List<Skill>
            {
                new SnapAim(mods, true),
                new SnapAim(mods, false),
                new FlowAim(mods, true),
                new FlowAim(mods, false),
                new Speed(mods)
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods));

            return skills.ToArray();
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new OsuModHidden(),
        };
    }
}
