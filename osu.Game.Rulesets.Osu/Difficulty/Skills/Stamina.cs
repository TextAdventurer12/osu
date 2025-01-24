// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Stamina : OsuStrainSkill
    {
        public Stamina(Mod[] mods)
            : base(mods)
        {
        }

        protected override double K => 2;
        protected override double Multiplier => 1;

        protected override void CalculateDifficulties()
        {
            return;
        }

        protected override double StrainDecayBase => 0.5;

        public override void Process(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            CurrentDifficulty = SpeedEvaluator.EvaluateDifficultyOf(osuCurrObj);
            Difficulties.Add(StrainValueAt(osuCurrObj));
        }
    }
}
