// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class Agility : OsuStrainSkill
    {
        protected Agility(Mod[] mods)
            : base(mods)
        {
        }

        protected override double K => 2;
        protected override double Multiplier => 1;

        /// <summary>
        /// TODO: remove all difficulties were it is easier to use flow aim
        /// </summary>
        protected override void CalculateDifficulties()
        {
            return;
        }

        public override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            CurrentDifficulty = AgilityEvaluator.EvaluateDifficultyOf(osuCurrObj);
            Difficulties.Add(StrainValueAt(osuCurrObj));
        }
    }
}
