// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

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
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Lists;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double aim_multiplier = 0.641;
        private const double tap_multiplier = 0.641;
        private const double finger_control_multiplier = 1.245;

        private const double star_rating_exponent = 0.83;

        public override int Version => 20220902;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }
        public override DifficultyAttributes Calculate([NotNull] IEnumerable<Mod> mods, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preProcess(mods, cancellationToken);

            var skills = CreateSkills(Beatmap, playableMods, clockRate);

            return CreateDifficultyAttributes(Beatmap, playableMods, skills, clockRate);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            var hitObjects = beatmap.HitObjects as List<OsuHitObject>;

            double mapLength = 0;
            if (beatmap.HitObjects.Count > 0)
                mapLength = (beatmap.HitObjects.Last().StartTime - beatmap.HitObjects.First().StartTime) / 1000 / clockRate;

            double preemptNoClockRate = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450);
            var noteDensities = NoteDensity.Calculate(hitObjects, preemptNoClockRate);

            // Tap
            var tapAttributes = Tap.CalculateTapAttributes(hitObjects, clockRate);

            // Finger Control
            double fingerControlDiff = FingerControl.CalculateFingerControlDiff(hitObjects, clockRate);

            // Aim
            var aimAttributes = Aim.CalculateAimAttributes(hitObjects, clockRate, tapAttributes.StrainHistory, noteDensities);

            double tapStarRating = tap_multiplier * Math.Pow(tapAttributes.TapDifficulty, star_rating_exponent);
            double aimStarRating = aim_multiplier * Math.Pow(aimAttributes.FcProbabilityThroughput, star_rating_exponent);
            double fingerControlStarRating = finger_control_multiplier * Math.Pow(fingerControlDiff, star_rating_exponent);
            double combinedStarRating = PowerMean.Of(new[] { tapStarRating, aimStarRating, fingerControlStarRating }, 7) * 1.131;

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(hitWindows.WindowFor(HitResult.Great)) / clockRate;
            double preempt = (int)IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            int beatmapMaxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the "headcircle" would be counted twice (once for the slider itself in the line above)
            beatmapMaxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            return new OsuDifficultyAttributes
            {
                StarRating = combinedStarRating,
                Mods = mods,
                Length = mapLength,

                TapStarRating = tapStarRating,
                TapDifficulty = tapAttributes.TapDifficulty,
                StreamNoteCount = tapAttributes.StreamNoteCount,
                MashTapDifficulty = tapAttributes.MashedTapDifficulty,

                FingerControlStarRating = fingerControlStarRating,
                FingerControlDifficulty = fingerControlDiff,

                AimStarRating = aimStarRating,
                AimDifficulty = aimAttributes.FcProbabilityThroughput,
                AimHiddenFactor = aimAttributes.HiddenFactor,
                ComboThroughputs = aimAttributes.ComboThroughputs,
                MissThroughputs = aimAttributes.MissThroughputs,
                MissCounts = aimAttributes.MissCounts,
                CheeseNoteCount = aimAttributes.CheeseNoteCount,
                CheeseLevels = aimAttributes.CheeseLevels,
                CheeseFactors = aimAttributes.CheeseFactors,

                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = beatmapMaxCombo,
                TotalObjectCount = beatmap.HitObjects.Count,
                HitCircleCount = hitCirclesCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            throw new NotImplementedException();
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[0];

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new MultiMod(new OsuModFlashlight(), new OsuModHidden())
        };
    }
}
