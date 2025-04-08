// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class OsuTapSkill : Skill
    {
        private const double summation_base = 0.9;
        public OsuTapSkill(Mod[] mods)
            : base(mods)
        {

        }
        List<Island> islands;
        Island currIsland;
        List<double> difficulties;

        protected abstract double DifficultyOf(Island island);

        /// <summary>
        /// Organises objects into islands which are then easier to handle when difficulty is calculated.
        /// TODO: make island detection smarter, e.g: 
        /// currently a triple is stored with the first note inside the previous island, a 2 note island, and then future notes in their own island.
        /// Ideally, a triple should be stored as a single island
        /// </summary>
        public override void Process(DifficultyHitObject obj)
        {
            OsuDifficultyHitObject osuObj = (OsuDifficultyHitObject)obj;
            if (currIsland.Count == 1 || DifficultyCalculationUtils.SimilarRhythms(osuObj.StrainTime, currIsland.Time))
            {
                currIsland.Add(osuObj);
                return;
            }
            islands.Add(currIsland);
            currIsland = new Island();
            currIsland.Add(osuObj);
            return;

        }
        public override double DifficultyValue()
        {
            difficulties.SortDescending();
            return difficulties.Sum((d, i) => d * Math.Pow(summation_base, i));
        }
        protected class Island
        {
            public List<OsuDifficultyHitObject> objects = new List<OsuDifficultyHitObject>();

            public Island()
            {
            }

            public int Count => objects.Count();

            /// <summary>
            /// Time between this island and the previous
            /// </summary>
            public double Interval
                => objects.Count() == 0 ? 0 : objects[0].StrainTime;

            /// <summary>
            /// Time between objects within this island
            /// </summary>
            public double Time
                => objects.Where((x, i) => i > 0).Average();
            public void Add(OsuDifficultyHitObject obj)
            {
                objects.Add(obj);
            }
        }
    }
}