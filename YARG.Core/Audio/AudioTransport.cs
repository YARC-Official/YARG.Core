using System;
using System.Collections.Generic;

namespace YARG.Core.Audio
{
    /// <summary>
    /// Family of the output transport: which driver/backend world a device belongs to.
    /// </summary>
    public enum AudioOutputBackend
    {
        WindowsAudio,
        Asio,
    }

    public readonly struct AudioTransportDescriptor
    {
        /// <summary>Stable identity, independent of display name.</summary>
        public string Id { get; }

        /// <summary>User-facing name. Also the legacy serialized device name.</summary>
        public string DisplayName { get; }

        /// <summary>Driver family this transport belongs to.</summary>
        public AudioOutputBackend Backend { get; }

        public AudioTransportDescriptor(string id, string displayName, AudioOutputBackend backend)
        {
            Id = id;
            DisplayName = displayName;
            Backend = backend;
        }
    }

    public readonly struct AudioInputDescriptor
    {
        /// <summary>Stable identity, independent of display name.</summary>
        public string Id { get; }

        /// <summary>User-facing name. Also the legacy serialized microphone name.</summary>
        public string DisplayName { get; }

        /// <summary>Transport-local channel/device index.</summary>
        public int ChannelId { get; }

        public AudioInputDescriptor(string id, string displayName, int channelId)
        {
            Id = id;
            DisplayName = displayName;
            ChannelId = channelId;
        }
    }

    public readonly struct AudioTransportConfiguration
    {
        /// <summary>Driver callback buffer length in frames. 0 = driver default.</summary>
        public int BufferLength { get; }

        public AudioTransportConfiguration(int bufferLength)
        {
            BufferLength = bufferLength;
        }
    }

    /// <summary>
    /// A complete audio endpoint: an output device plus the inputs that belong to it.
    ///
    /// Transports own their driver-specific policy: buffer configuration, control panel,
    /// driver notifications, and input enumeration/acquisition. The audio manager only
    /// orchestrates activation and routing, and never branches on transport type.
    /// </summary>
    public abstract class AudioTransport : IDisposable
    {
        private bool _disposed;

        public abstract AudioTransportDescriptor Descriptor { get; }

        /// <summary>Device that mixers route to while this transport is active.</summary>
        public abstract OutputDevice MixerDevice { get; }

        /// <summary>
        /// Brings the transport up: initializes the output context and starts output.
        /// One-shot per instance; a failed activation must leave no resources behind.
        /// </summary>
        public abstract bool Activate(AudioTransportConfiguration configuration);

        /// <summary>
        /// Tears the transport down: stops output, disposes the context, invalidates inputs.
        /// Idempotent. Mixers must be moved away and routes detached before calling.
        /// </summary>
        public abstract void Deactivate();

        /// <summary>Inputs available from this transport while active.</summary>
        public abstract IReadOnlyList<AudioInputDescriptor> GetInputs();

        /// <summary>Creates a mic device for one of <see cref="GetInputs"/>' descriptors.</summary>
        public abstract MicDevice? CreateInput(AudioInputDescriptor descriptor);

        /// <summary>Buffer configuration support. null = no buffer control.</summary>
        public virtual OutputBufferInfo? GetBufferInfo() => null;

        /// <summary>Opens the driver's control panel. false = no panel.</summary>
        public virtual bool OpenControlPanel() => false;

        /// <summary>
        /// Raised when the transport itself requests a rebuild (driver settings changed,
        /// device lost). Raised on the main thread.
        /// </summary>
        public event Action? ReinitializeRequested;

        protected void NotifyReinitializeRequested() => ReinitializeRequested?.Invoke();

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

        ~AudioTransport()
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
