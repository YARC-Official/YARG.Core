using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class BackgroundResult
    {
        public readonly BackgroundType Type;
        public readonly Stream? Stream;

        public BackgroundResult(BackgroundType type, Stream? stream)
        {
            Type = type;
            Stream = stream;
        }
    }

    public abstract partial class SongEntry
    {
        public abstract SongChart? LoadChart();
        public abstract StemMixer? LoadAudio(float speed, params SongStem[] ignoreStems);
        public abstract byte[]? LoadAlbumData();
        public abstract BackgroundResult? LoadBackground(BackgroundType options);
        public abstract byte[]? LoadMiloData();

        public async Task<PreviewContext?> LoadPreview(float volume, float speed, CancellationTokenSource token)
        {
            try
            {
                // Wait for a X milliseconds to prevent spam loading (no one likes Music Library lag)
                await Task.Delay(TimeSpan.FromMilliseconds(400.0));

                // Check if cancelled
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                // Load the song
                var mixer = await Task.Run(() => LoadPreviewMixer(speed));
                if (mixer == null || token.IsCancellationRequested)
                {
                    mixer?.Dispose();
                    return null;
                }

                double audioLength = mixer.Length;
                double previewStartTime, previewEndTime;
                if (mixer.Channels.Count > 0)
                {
                    // Set preview start and end times
                    previewStartTime = PreviewStartSeconds;
                    if (previewStartTime <= 0.0 || previewStartTime >= audioLength)
                    {
                        if (20 <= audioLength)
                            previewStartTime = 10;
                        else
                            previewStartTime = audioLength / 2;
                    }

                    previewEndTime = PreviewEndSeconds;
                    if (previewEndTime <= 0.0 || previewEndTime + 1 >= audioLength)
                    {
                        previewEndTime = previewStartTime + PreviewContext.DEFAULT_PREVIEW_DURATION;
                        if (previewEndTime + 1 > audioLength)
                            previewEndTime = audioLength - 1;
                    }
                }
                else
                {
                    previewStartTime = 0;
                    previewEndTime = audioLength - 1;
                }
                return new PreviewContext(mixer, previewStartTime, previewEndTime, volume, token);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while loading song preview!");
                return null;
            }
        }

        protected abstract StemMixer? LoadPreviewMixer(float speed);
    }
}
