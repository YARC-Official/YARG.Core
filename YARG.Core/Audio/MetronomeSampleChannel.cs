using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class MetronomeSampleChannel : IDisposable
    {
        private      bool _disposed;

        protected readonly string _hiPath;
        protected readonly string _loPath;
        protected          double _volume = 1f;

        public readonly MetronomeSample Sample;

        protected MetronomeSampleChannel(MetronomeSample sample, string hiPath, string loPath)
        {
            Sample = sample;
            _hiPath = hiPath;
            _loPath = loPath;
            GlobalAudioHandler.StemSettings[SongStem.Metronome].OnVolumeChange += SetVolume;
        }

        public void PlayHi()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    PlayHi_Internal();
                }
            }
        }

        public void PlayLo()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    PlayLo_Internal();
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

        internal void SetOutputChannel(OutputChannel channel)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetOutputChannel_Internal(channel);
                }
            }
        }

        protected abstract void PlayHi_Internal();
        protected abstract void PlayLo_Internal();
        protected abstract void SetVolume_Internal(double volume);

        protected abstract void SetOutputChannel_Internal(OutputChannel? channel);

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