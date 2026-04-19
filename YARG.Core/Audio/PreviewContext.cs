using System;
using System.Threading.Tasks;
using System.Threading;
using YARG.Core.Logging;
using YARG.Core.Song;
using System.Diagnostics;

namespace YARG.Core.Audio
{
    public class PreviewContext : IDisposable
    {
        private const double DEFAULT_PREVIEW_DURATION = 30.0;
        private const double DEFAULT_START_TIME = 20.0;
        private const double DEFAULT_END_TIME = 50.0;

        public static async Task<PreviewContext?> Create(
            SongEntry entry,
            float volume,
            float speed,
            double delaySeconds,
            double fadeDuration,
            CancellationToken token)
        {
            try
            {
                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }

                // Check if canceled
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                // Load the song
                var mixer = await Task.Run(() => entry.LoadPreviewAudio(speed), token);
                if (mixer == null || token.IsCancellationRequested)
                {
                    mixer?.Dispose();
                    return null;
                }

                double previewLength = mixer.Length;
                double previewStartTime = 0;
                if (mixer.Channels.Count > 0)
                {
                    double previewEndTime;
                    if ((entry.PreviewStartMilliseconds < 0 || entry.PreviewStartSeconds >= previewLength)
                    &&  (entry.PreviewEndMilliseconds <= 0  || entry.PreviewEndSeconds > previewLength))
                    {
                        if (DEFAULT_END_TIME <= previewLength)
                        {
                            previewStartTime = DEFAULT_START_TIME;
                            previewEndTime = DEFAULT_END_TIME;
                        }
                        else if (DEFAULT_PREVIEW_DURATION <= previewLength)
                        {
                            previewStartTime = (previewLength - DEFAULT_PREVIEW_DURATION) / 2;
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }
                        else
                        {
                            previewStartTime = 0;
                            previewEndTime = previewLength;
                        }
                    }
                    else if (0 <= entry.PreviewStartSeconds && entry.PreviewStartSeconds < previewLength)
                    {
                        previewStartTime = entry.PreviewStartSeconds;
                        previewEndTime = entry.PreviewEndSeconds;
                        if (previewEndTime <= previewStartTime)
                        {
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }

                        if (previewEndTime > previewLength)
                        {
                            previewEndTime = previewLength;
                        }
                    }
                    else
                    {
                        previewEndTime = entry.PreviewEndSeconds;
                        previewStartTime = previewEndTime - DEFAULT_PREVIEW_DURATION;
                        if (previewStartTime < 0)
                        {
                            previewStartTime = 0;
                        }
                    }
                    previewLength = previewEndTime - previewStartTime;
                }

                if (fadeDuration > previewLength / 4)
                {
                    fadeDuration = previewLength / 4;
                }
                return new PreviewContext(mixer, previewStartTime, previewLength, fadeDuration, volume, token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while loading song preview!");
                return null;
            }
        }

        private readonly StemMixer         _mixer;
        private readonly Task              _task;
        private readonly double            _previewStartTime;
        private readonly double            _previewLength;
        private readonly double            _fadeDuration;
        private readonly float             _volume;
        private readonly CancellationToken _token;
        private          bool              _disposed;

        private PreviewContext(
            StemMixer mixer,
            double previewStartTime,
            double previewLength,
            double fadeDuration,
            float volume,
            CancellationToken token)
        {
            _mixer = mixer;
            _previewStartTime = previewStartTime;
            _previewLength = previewLength;
            _fadeDuration = fadeDuration;
            _volume = volume;
            _token = token;

            _task = Task.Run(Loop);
        }

        public async Task WaitForCompletionAsync()
        {
            await _task;
        }

        private async Task Loop()
        {
            try
            {
                var watch = new Stopwatch();
                while (true)
                {
                    _mixer.SetPosition(_previewStartTime);
                    _mixer.FadeIn(_volume, _fadeDuration);
                    _mixer.Play();
                    watch.Restart();
                    while (watch.Elapsed.TotalSeconds < _previewLength - _fadeDuration && !_token.IsCancellationRequested)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                        // ReSharper disable once MethodSupportsCancellation
                        await Task.Delay(1);
                    }

                    watch.Restart();
                    _mixer.FadeOut(_fadeDuration);
                    while (watch.Elapsed.TotalSeconds < _fadeDuration)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                        // ReSharper disable once MethodSupportsCancellation
                        await Task.Delay(1);
                    }

                    _mixer.Pause();
                    if (_token.IsCancellationRequested)
                    {
                        Dispose();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while looping song preview!");
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (disposing)
            {
                _mixer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}