using System;
using System.Threading.Tasks;
using System.Threading;
using YARG.Core.Logging;
using YARG.Core.Song;

namespace YARG.Core.Audio
{
    public class PreviewContext : IDisposable
    {
        private const double DEFAULT_PREVIEW_DURATION = 30.0;
        private const double DEFAULT_START_TIME = 20.0;
        private const double DEFAULT_END_TIME = 50.0;

        public static async Task<PreviewContext?> Create(SongEntry entry, float volume, float speed, double delaySeconds, double fadeDuration, CancellationTokenSource token)
        {
            try
            {
                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }

                // Check if cancelled
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                // Load the song
                var mixer = await Task.Run(() => entry.LoadPreviewAudio(speed));
                if (mixer == null || token.IsCancellationRequested)
                {
                    mixer?.Dispose();
                    return null;
                }

                double audioLength = mixer.Length;
                double previewStartTime, previewEndTime;
                if (mixer.Channels.Count > 0)
                {
                    if ((entry.PreviewStartSeconds < 0 || entry.PreviewStartSeconds >= audioLength)
                    &&  (entry.PreviewEndSeconds < 0   || entry.PreviewEndSeconds >= audioLength))
                    {
                        if (DEFAULT_END_TIME <= audioLength)
                        {
                            previewStartTime = DEFAULT_START_TIME;
                            previewEndTime = DEFAULT_END_TIME;
                        }
                        else if (DEFAULT_PREVIEW_DURATION <= audioLength)
                        {
                            previewStartTime = (audioLength - DEFAULT_PREVIEW_DURATION) / 2;
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }
                        else
                        {
                            previewStartTime = 0;
                            previewEndTime = DEFAULT_PREVIEW_DURATION;
                        }
                    }
                    else if (0 <= entry.PreviewStartSeconds && entry.PreviewStartSeconds < audioLength)
                    {
                        previewStartTime = entry.PreviewStartSeconds;
                        previewEndTime = entry.PreviewEndSeconds;
                        if (previewEndTime <= previewStartTime)
                        {
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }

                        if (previewEndTime > audioLength)
                        {
                            previewEndTime = audioLength;
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
                }
                else
                {
                    previewStartTime = 0;
                    previewEndTime = audioLength;
                }
                previewEndTime -= fadeDuration;
                return new PreviewContext(mixer, previewStartTime, previewEndTime, fadeDuration, volume, token);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while loading song preview!");
                return null;
            }
        }

        private StemMixer _mixer;
        private Task _task;
        private readonly double _previewStartTime;
        private readonly double _previewEndTime;
        private readonly double _fadeDruation;
        private readonly float _volume;
        private readonly CancellationTokenSource _token;
        private bool _disposed;

        public bool IsPlaying => !_token.IsCancellationRequested;

        private PreviewContext(StemMixer mixer, double previewStartTime, double previewEndTime, double fadeDuration, float volume, CancellationTokenSource token)
        {
            _mixer = mixer;
            _previewStartTime = previewStartTime;
            _previewEndTime = previewEndTime;
            _fadeDruation = fadeDuration;
            _volume = volume;
            _token = token;

            _task = Task.Run(Loop);
        }

        public async void Stop()
        {
            _token.Cancel();
            await _task;
        }

        private enum LoopStage
        {
            FadeIn,
            Main,
            FadeOut
        }

        private async void Loop()
        {
            try
            {
                // Preview mixers start at a volume of zero
                var stage = LoopStage.FadeIn;
                while (!_disposed)
                {
                    switch (stage)
                    {
                        case LoopStage.FadeIn:
                            _mixer.SetPosition(_previewStartTime);
                            _mixer.FadeIn(_volume, _fadeDruation);
                            _mixer.Play();
                            stage = LoopStage.Main;
                            break;
                        case LoopStage.Main:
                            if (_mixer.GetPosition() < _previewEndTime && !_token.IsCancellationRequested)
                            {
                                break;
                            }

                            _mixer.FadeOut(_fadeDruation);
                            stage = LoopStage.FadeOut;
                            break;
                        case LoopStage.FadeOut:
                            if (_mixer.GetVolume() >= 0.01f)
                            {
                                break;
                            }

                            _mixer.Pause();
                            if (_token.IsCancellationRequested)
                            {
                                Dispose();
                                return;
                            }
                            stage = LoopStage.FadeIn;
                            break;
                    }
                    await Task.Delay(25);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while looping song preview!");
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _mixer.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
