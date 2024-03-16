using System;

namespace YARG.Core.Audio
{
    public abstract class StemChannel : IDisposable
    {
        private bool _disposed;

        protected readonly AudioManager _manager;
        protected double _volume;

        public readonly SongStem Stem;
        public double Volume => _volume;

        protected StemChannel(AudioManager manager, SongStem stem, double volume)
        {
            _manager = manager;
            _volume = volume;
            Stem = stem;
        }


        public abstract void SetVolume(double newVolume);

        public abstract void SetReverb(bool reverb);

        public abstract void SetWhammyPitch(float percent);
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
