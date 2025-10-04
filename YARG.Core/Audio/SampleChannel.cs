using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class SampleChannel : IDisposable
    {
        protected const double PLAYBACK_SUPPRESS_THRESHOLD = 0.05f;
        private bool _disposed;

        protected readonly string _path;
        protected readonly int _playbackCount;

        public readonly SfxSample Sample;
        protected SampleChannel(SfxSample sample, string path, int playbackCount)
        {
            Sample = sample;
            _path = path;
            _playbackCount = playbackCount;

            GlobalAudioHandler.StemSettings[SongStem.Sfx].OnVolumeChange += SetVolume;
        }

        public void Play(double duration = 0)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Play_Internal(duration);
                }
            }
        }

        // TODO: Implement properly (fade out when duration approaches if sample is still playing)
        public void PlayForTime(double duration)
        {
            Play();
        }

        public void Stop(double duration = 0)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Stop_Internal(duration);
                }
            }
        }

        public void Pause()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Pause_Internal();
                }
            }
        }

        public void Resume()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Resume_Internal();
                }
            }
        }

        private void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(volume);
                }
            }
        }

        protected void SetEndCallback()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetEndCallback_Internal();
                }
            }
        }

        protected void EndCallback(int _, int __, int ___, IntPtr ____)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    EndCallback_Internal(_, __, ___, ____);
                }
            }
        }

        protected abstract void Play_Internal(double duration);
        protected abstract void Stop_Internal(double duration);
        protected abstract void Pause_Internal();
        protected abstract void Resume_Internal();
        protected abstract void SetVolume_Internal(double volume);
        protected abstract void SetEndCallback_Internal();
        protected abstract void EndCallback_Internal(int _, int __, int ___, IntPtr ____);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    GlobalAudioHandler.StemSettings[SongStem.Sfx].OnVolumeChange -= SetVolume;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~SampleChannel()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
