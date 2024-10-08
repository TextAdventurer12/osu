// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Online.Rooms;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Components
{
    public abstract partial class OnlinePlayBackgroundScreen : BackgroundScreen
    {
        private CancellationTokenSource? cancellationSource;
        private PlaylistItemBackground? background;

        [BackgroundDependencyLoader]
        private void load()
        {
            switchBackground(new PlaylistItemBackground(playlistItem));
        }

        private PlaylistItem? playlistItem;

        protected PlaylistItem? PlaylistItem
        {
            get => playlistItem;
            set
            {
                if (playlistItem == value)
                    return;

                playlistItem = value;

                if (LoadState > LoadState.Ready)
                    updateBackground();
            }
        }

        private void updateBackground()
        {
            Schedule(() =>
            {
                var beatmap = playlistItem?.Beatmap;

                string? lastCover = (background?.Beatmap?.BeatmapSet as IBeatmapSetOnlineInfo)?.Covers.Cover;
                string? newCover = (beatmap?.BeatmapSet as IBeatmapSetOnlineInfo)?.Covers.Cover;

                if (lastCover == newCover)
                    return;

                cancellationSource?.Cancel();
                LoadComponentAsync(new PlaylistItemBackground(playlistItem), switchBackground, (cancellationSource = new CancellationTokenSource()).Token);
            });
        }

        private void switchBackground(PlaylistItemBackground newBackground)
        {
            float newDepth = 0;

            if (background != null)
            {
                newDepth = background.Depth + 1;
                background.FinishTransforms();
                background.FadeOut(250);
                background.Expire();
            }

            newBackground.Depth = newDepth;
            newBackground.Colour = ColourInfo.GradientVertical(new Color4(0.1f, 0.1f, 0.1f, 1f), new Color4(0.4f, 0.4f, 0.4f, 1f));
            newBackground.BlurTo(new Vector2(10));

            AddInternal(background = newBackground);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            this.MoveToX(0, TRANSITION_LENGTH);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            bool result = base.OnExiting(e);
            this.MoveToX(0);
            return result;
        }
    }
}
