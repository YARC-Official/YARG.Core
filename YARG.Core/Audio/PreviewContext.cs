using System;
using System.Threading.Tasks;
using System.Threading;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public class PreviewContext : IDisposable
    {
        public const double DEFAULT_PREVIEW_DURATION = 30.0;

        private StemMixer _mixer;
        private Task _task;
        private readonly double _previewStartTime;
        private readonly double _previewEndTime;
        private readonly float _volume;
        private readonly CancellationTokenSource _token;
        private bool _disposed;

        public bool IsPlaying => !_token.IsCancellationRequested;

        public PreviewContext(StemMixer mixer, double previewStartTime, double previewEndTime, float volume, CancellationTokenSource token)
        {
            _mixer = mixer;
            _previewStartTime = previewStartTime;
            _previewEndTime = previewEndTime;
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

        private void Loop()
        {
            try
            {
                var stage = LoopStage.FadeIn;
                _mixer.SetVolume(0);
                while (!_disposed)
                {
                    switch (stage)
                    {
                        case LoopStage.FadeIn:
                            _mixer.SetPosition(_previewStartTime);
                            _mixer.FadeIn(_volume);
                            _mixer.Play();
                            stage = LoopStage.Main;
                            break;
                        case LoopStage.Main:
                            if (_mixer.GetPosition() < _previewEndTime && !_token.IsCancellationRequested)
                            {
                                break;
                            }

                            _mixer.FadeOut();
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
                    Task.Delay(25);
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

        ~PreviewContext()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
