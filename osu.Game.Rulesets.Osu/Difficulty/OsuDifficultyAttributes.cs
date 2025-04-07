// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        /// <summary>
        /// The difficulty of aiming all of the objects that are 'snapped'
        /// </summary>
        [JsonProperty("snap_aim")]
        public double SnapAim { get; set; }

        /// <summary>
        /// The difficulty of aiming all of the objects that are 'flowed'
        /// </summary>
        [JsonProperty("flow_aim")]
        public double FlowAim { get; set; }

        /// <summary>
        /// The difficulty of aiming snapped objects based on their speed
        /// </summary>
        [JsonProperty("agility")]
        public double Agility { get; set; }

        /// <summary>
        /// The difficulty of tapping very fast objects (typically relevant on less than 12 notes)
        /// </summary>
        [JsonProperty("raw_speed")]
        public double RawSpeed { get; set; }

        /// <summary>
        /// The difficulty of tapping objects that are fast and somewhat long (relevant on 12 notes to ~100 notes)
        /// </summary>
        [JsonProperty("speed")]
        public double Speed { get; set; }

        /// <summary>
        /// The difficulty of tapping many objects in succession, resulting in significant strain (upwards of 100 notes)
        /// </summary>
        [JsonProperty("stamina")]
        public double Stamina { get; set; }

        /// <summary>
        /// The difficulty of tapping rhythmically complex notes
        /// </summary>
        [JsonProperty("finger_control")]
        public double FingerControl { get; set; }

        /// <summary>
        /// The difficulty of obtaining high accuracy on the objects in the map
        /// TODO: this should be a set of coeffs and work as a scaling based on 100 count, not a value.
        /// </summary>
        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }

        /// <summary>
        /// The difficulty relating to observation of objects
        /// </summary>
        [JsonProperty("reading")]
        public double Reading { get; set; }
        
        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            throw new NotImplementedException("calm your farm");
        }

        #region Newtonsoft.Json implicit ShouldSerialize() methods

        // The properties in this region are used implicitly by Newtonsoft.Json to not serialise certain fields in some cases.
        // They rely on being named exactly the same as the corresponding fields (casing included) and as such should NOT be renamed
        // unless the fields are also renamed.

        [UsedImplicitly]
        public bool ShouldSerializeFlashlightDifficulty() => Mods.Any(m => m is ModFlashlight);

        #endregion
    }
}
