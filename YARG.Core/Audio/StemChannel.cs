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

            var settings = AudioManager.StemSettings[Stem];
            settings.OnVolumeChange += SetVolume;
            settings.OnReverbChange += SetReverb;
        }

        public abstract void SetWhammyPitch(float percent);
        public abstract void SetPosition(double position, bool bufferCompensation = true);

        protected abstract void SetVolume(double newVolume);
        protected abstract void SetReverb(bool reverb);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                AudioManager.StemSettings[Stem].OnVolumeChange -= SetVolume;
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
