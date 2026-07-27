using System;

namespace YARG.Core.Audio
{
    public abstract class VenueSampleChannel : IDisposable
    {
        private bool _disposed;

        private         byte[] _sampleData;
        public readonly string SampleName;

        public VenueSampleChannel(string sampleName, byte[] sampleData)
        {
            SampleName = sampleName;
            _sampleData = sampleData;

            GlobalAudioHandler.StemSettings[SongStem.VoxSample].OnVolumeChange += SetVolume;
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

        public void Pause()
        {
            if (!IsPlaying())
            {
                return;
            }

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
            if (!IsPaused())
            {
                return;
            }

            lock (this)
            {
                if (!_disposed)
                {
                    Resume_Internal();
                }
            }
        }

        public void Stop()
        {
            if (!IsPlaying() && !IsPaused())
            {
                return;
            }

            lock (this)
            {
                if (!_disposed)
                {
                    Stop_Internal();
                }
            }
        }

        public bool IsPlaying()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    return IsPlaying_Internal();
                }
            }

            return false;
        }

        public bool IsPaused()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    return IsPaused_Internal();
                }
            }

            return false;
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

        protected abstract void Play_Internal();
        protected abstract void Stop_Internal();
        protected abstract void Pause_Internal();
        protected abstract void Resume_Internal();
        protected abstract void SetVolume_Internal(double volume);
        protected abstract void SetEndCallback_Internal();
        protected abstract void EndCallback_Internal(int _, int __, int ___, IntPtr ____);
        protected abstract void SetOutputChannel_Internal(OutputChannel? channel);
        protected abstract bool IsPlaying_Internal();
        protected abstract bool IsPaused_Internal();

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    GlobalAudioHandler.StemSettings[SongStem.VoxSample].OnVolumeChange -= SetVolume;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }

                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~VenueSampleChannel()
        {
            Dispose(false);
        }
    }
}