using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class MetronomeSampleChannel : IDisposable
    {
        public const int  ROUND_ROBIN_MAX_INDEX = 3;
        private      bool _disposed;

        protected readonly string _path;
        protected          double _volume = 1f;

        public readonly MetronomeSample Sample;

        protected MetronomeSampleChannel(MetronomeSample sample, string path)
        {
            Sample = sample;
            _path = path;
            GlobalAudioHandler.StemSettings[SongStem.Metronome].OnVolumeChange += SetVolume;
        }

        public void Play()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Play_Internal();
                }
            }
        }

        private void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    volume *= _volume;
                    SetVolume_Internal(volume);
                }
            }
        }

        protected abstract void Play_Internal();
        protected abstract void SetVolume_Internal(double volume);

        protected virtual void DisposeManagedResources()
        {
        }

        protected virtual void DisposeUnmanagedResources()
        {
        }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    GlobalAudioHandler.StemSettings[SongStem.Metronome].OnVolumeChange -= SetVolume;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }

                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~MetronomeSampleChannel()
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