using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class SampleChannel : IDisposable
    {
        protected const double PLAYBACK_SUPPRESS_THRESHOLD = 0.05f;
        private bool disposedValue;

        protected readonly string _path;
        protected readonly int _playbackCount;

        public readonly SfxSample Sample;
        protected SampleChannel(SfxSample sample, string path, int playbackCount)
        {
            Sample = sample;
            _path = path;
            _playbackCount = playbackCount;
        }

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        public abstract void Play();

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeManagedResources();
                }
                DisposeUnmanagedResources();
                disposedValue = true;
            }
        }

        ~SampleChannel()
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
