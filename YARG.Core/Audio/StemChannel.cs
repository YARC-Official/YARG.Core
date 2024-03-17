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

        public void SetWhammyPitch(float percent)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetWhammyPitch_Internal(percent);
                }
            }
        }

        public void SetPosition(double position, bool bufferCompensation = true)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetPosition_Internal(position, bufferCompensation);
                }
            }
        }

        public void SetSpeed(float speed)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetSpeed_Internal(speed);
                }
            }
        }

        private void SetVolume(double newVolume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(newVolume);
                }
            }
        }

        private void SetReverb(bool reverb)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetReverb_Internal(reverb);
                }
            }
        }

        protected abstract void SetWhammyPitch_Internal(float percent);
        protected abstract void SetPosition_Internal(double position, bool bufferCompensation);
        protected abstract void SetSpeed_Internal(float speed);

        protected abstract void SetVolume_Internal(double newVolume);
        protected abstract void SetReverb_Internal(bool reverb);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
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
