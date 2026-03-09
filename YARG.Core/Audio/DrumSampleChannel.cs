using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class DrumSampleChannel : IDisposable
    {
        public const int ROUND_ROBIN_MAX_INDEX = 3;
        private bool _disposed;

        protected readonly string _path;
        protected readonly int _playbackCount;
        protected double _volume;
        private double _settingVolume = 1;

        public readonly DrumSfxSample Sample;
        protected DrumSampleChannel(DrumSfxSample sample, string path, int playbackCount)
        {
            Sample = sample;
            _path = path;
            _playbackCount = playbackCount;
        }

        public void Play(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    _volume = volume;
                    SetVolume_Internal(volume * _settingVolume);
                    Play_Internal();
                }
            }
        }

        internal void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    _settingVolume = volume;
                    SetVolume_Internal(volume * _volume);
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

        protected abstract void Play_Internal();
        protected abstract void SetVolume_Internal(double volume);
        protected abstract void SetOutputChannel_Internal(OutputChannel? channel);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~DrumSampleChannel()
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
