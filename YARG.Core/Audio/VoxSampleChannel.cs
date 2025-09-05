using System;

namespace YARG.Core.Audio
{
    /// <summary>
    /// A sample channel that uses BASS to play VOX files.
    ///
    /// Unlike all the others, this one will automatically queue samples and play them sequentially,
    /// so don't be surprised when you can't play overlapping vox clips
    /// </summary>
    public abstract class VoxSampleChannel : IDisposable
    {
        private bool _disposed;

        protected readonly string    _path;
        public readonly    VoxSample Sample;

        protected VoxSampleChannel(VoxSample sample, string path)
        {
            Sample = sample;
            _path = path;

            GlobalAudioHandler.StemSettings[SongStem.VoxSample].OnVolumeChange += SetVolume;
        }

        public string Path => _path;

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
                    SetVolume_Internal(volume);
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

        protected abstract void Play_Internal();
        protected abstract void SetVolume_Internal(double volume);
        protected abstract bool IsPlaying_Internal();

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

        ~VoxSampleChannel()
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
