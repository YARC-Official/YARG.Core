using System;

namespace YARG.Core.Audio
{
    /// <summary>
    ///     A live (streamed) audio source that the application feeds with decoded PCM samples.
    ///     The underlying implementation mixes the stream into the main output like any other
    ///     decode source, so output device changes are handled by the existing router path.
    /// </summary>
    public abstract class PushStream : IDisposable
    {
        private bool _disposed;

        public readonly int Handle;
        public readonly int SampleRate;
        public readonly int ChannelCount;


        protected PushStream(int handle, int sampleRate, int channelCount)
        {
            Handle = handle;
            SampleRate = sampleRate;
            ChannelCount = channelCount;
        }

        /// <summary>
        ///     Appends interleaved float32 sample data to the stream. The pointer must remain valid for
        ///     the duration of the call only; the data is copied internally.
        /// </summary>
        public void Push(IntPtr bufferPtr, int byteLength)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Push_Internal(bufferPtr, byteLength);
                }
            }
        }

        public void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(volume);
                }
            }
        }

        /// <summary>
        ///     Discards all queued but unplayed samples, so a seek or stop in the source does not
        ///     leave stale audio in the output.
        /// </summary>
        public void Clear()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Clear_Internal();
                }
            }
        }

        protected abstract void Push_Internal(IntPtr bufferPtr, int byteLength);
        protected abstract void SetVolume_Internal(double volume);
        protected abstract void Clear_Internal();

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

        ~PushStream()
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
