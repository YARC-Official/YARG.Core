using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Core.Audio
{
    public abstract class StemChannel : IDisposable
    {
        private bool _disposed;

        protected readonly AudioManager _manager;
        protected double _volume;

        public readonly SongStem Stem;
        public readonly double LengthD;
        public float LengthF => (float) LengthD;
        public double Volume => _volume;

        public abstract event Action ChannelEnd;

        protected StemChannel(AudioManager manager, SongStem stem, double length, double volume)
        {
            _manager = manager;
            _volume = volume;

            Stem = stem;
            LengthD = length;
        }

        public abstract void FadeIn(float maxVolume);
        public abstract Task FadeOut();

        public abstract void SetVolume(double newVolume);

        public abstract void SetReverb(bool reverb);

        public abstract void SetSpeed(float speed);
        public abstract void SetWhammyPitch(float percent);

        public abstract double GetPosition(bool bufferCompensation = true);
        public abstract void SetPosition(double position, bool bufferCompensation = true);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
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

        ~StemChannel()
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
