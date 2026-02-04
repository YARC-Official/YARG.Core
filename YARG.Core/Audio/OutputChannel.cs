using System;

namespace YARG.Core.Audio
{
    public abstract class OutputChannel : IDisposable
    {
        private bool _disposed;

        public readonly int ChannelId;

        protected OutputChannel(int channelId)
        {
            ChannelId = channelId;
        }

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

        ~OutputChannel()
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
