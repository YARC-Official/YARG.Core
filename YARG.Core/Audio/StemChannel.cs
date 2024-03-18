using System;

namespace YARG.Core.Audio
{
    public abstract class StemChannel : IDisposable
    {
        private bool _disposed;

        protected readonly AudioManager _manager;
        public readonly SongStem Stem;

        protected StemChannel(AudioManager manager, SongStem stem)
        {
            _manager = manager;
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

        private void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    volume = AudioManager.ClampStemVolume(volume);
                    SetVolume_Internal(volume);
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
