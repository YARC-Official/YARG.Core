using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public readonly struct OutputBufferInfo
    {
        public int[] SupportedLengths { get; }
        public int PreferredLength { get; }
        public int SampleRate { get; }
        public bool IsDriverControlled { get; }

        public OutputBufferInfo(int[] supportedLengths, int preferredLength, int sampleRate, bool isDriverControlled)
        {
            SupportedLengths = supportedLengths;
            PreferredLength = preferredLength;
            SampleRate = sampleRate;
            IsDriverControlled = isDriverControlled;
        }
    }

    public abstract class AudioManager
    {
        private static float _globalSpeed = 1f;

        private bool _disposed;
        private List<StemMixer> _activeMixers = new();

        protected internal SampleChannel[]          SfxSamples       = new SampleChannel[AudioHelpers.SfxSamples.Count];
        protected internal DrumSampleChannel[]      DrumSfxSamples   = new DrumSampleChannel[AudioHelpers.DrumSamples.Count];
        protected internal VoxSampleChannel[]       VoxSamples       = new VoxSampleChannel[AudioHelpers.VoxSamples.Count];
        protected internal MetronomeSampleChannel[] MetronomeSamples = new MetronomeSampleChannel[AudioHelpers.MetronomeSamples.Count];
        protected internal Dictionary<string, VenueSampleChannel>  VenueSamples     = new();
        protected internal int PlaybackLatency;
        protected internal int MinimumBufferLength;
        protected internal int MaximumBufferLength;

        protected internal abstract ReadOnlySpan<string> SupportedFormats { get; }

        internal StemMixer? LoadCustomFile(string name, Stream stream, float speed, double volume, bool normalize, SongStem stem = SongStem.Song)
        {
            YargLogger.LogDebug("Loading custom audio file");
            var mixer = CreateMixer(name, speed, volume, clampStemVolume: false, normalize: normalize);
            if (mixer == null)
            {
                return null;
            }

            if (!mixer.AddChannel(stream, stem))
            {
                mixer.Dispose();
                return null;
            }
            YargLogger.LogDebug("Custom audio file loaded");
            return mixer;
        }

        internal StemMixer? LoadCustomFile(string file, float speed, double volume, bool normalize, SongStem stem = SongStem.Song)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            var mixer = LoadCustomFile(file, stream, speed, volume, normalize, stem);
            if (mixer == null)
            {
                YargLogger.LogFormatError("Failed to load audio file{0}!", file);
                stream.Dispose();
                return null;
            }
            return mixer;
        }

        protected internal abstract StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume, bool normalize);

        protected internal abstract List<InputDeviceInfo> GetAllInputDevices();

        protected internal abstract MicDevice? CreateInputDevice(InputDeviceInfo device);

        protected internal virtual MicDevice? GetInputDevice(string name)
        {
            if (InputDeviceInfo.TryParseDisplayName(name, out var baseName, out var channel))
            {
                return GetInputDevice(baseName, channel);
            }
            return null;
        }

        protected internal virtual MicDevice? GetInputDevice(string baseName, int channel)
        {
            var info = new InputDeviceInfo(-1, baseName, channel, channel + 1);
            return CreateInputDevice(info);
        }

        protected internal abstract OutputChannel? CreateOutputChannel(int channelId);

        protected internal abstract List<(int id, string name)> GetAllOutputDevices();

        protected internal abstract int GetOutputChannelCount();

        protected internal virtual OutputBufferInfo? GetOutputBufferInfo() => null;

        protected internal virtual bool OpenOutputControlPanel() => false;

        protected internal virtual void Update() { }

        /// <summary>
        /// The driver family a device name belongs to. Classification lives with the
        /// transport implementations, not with name parsing in callers.
        /// </summary>
        protected internal virtual AudioOutputMode GetOutputMode(string name) =>
            AudioOutputMode.Shared;

        protected internal abstract void SetMasterVolume(double volume);

        public abstract void LoadVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel = null);

        public abstract void ClearVenueSamples();

        protected internal abstract void PlayMetronomeSoundEffectToChannel(MetronomeSample sample,
            MetronomePitch pitch, int channelId);

        protected internal virtual void SetOutputChannel(OutputChannel channel)
        {
            foreach (StemMixer mixer in SnapshotActiveMixers())
            {
                mixer.SetOutputChannel(channel);
            }
        }

        protected internal abstract bool SetOutputDevice(string name);

        protected internal virtual bool ReinitializeOutput() => false;

        protected void MoveActiveMixersTo(OutputDevice device)
        {
            foreach (StemMixer mixer in SnapshotActiveMixers())
            {
                mixer.SetOutputDevice(device);
            }
        }


        internal void SetBufferLength(int length)
        {
            SetBufferLength_Internal(length);
            foreach (var mixer in SnapshotActiveMixers())
            {
                mixer.SetBufferLength(length);
            }
        }


        protected abstract void SetBufferLength_Internal(int length);

        internal float GlobalSpeed
        {
            get => _globalSpeed;
            set
            {
                if (_disposed || _globalSpeed == value)
                {
                    return;
                }

                _globalSpeed = value;
                foreach (var mixer in SnapshotActiveMixers())
                {
                    mixer.SetPlaybackSpeed(value);
                }
            }
        }

        private StemMixer[] SnapshotActiveMixers()
        {
            // Mixer disposal removes itself from this list while holding mixer lock.
            // Release list lock before calling into any mixer to avoid lock inversion.
            lock (_activeMixers)
            {
                return _activeMixers.ToArray();
            }
        }

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void AddMixer(StemMixer mixer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    mixer.Dispose();
                    return;
                }

                lock (_activeMixers)
                {
                    var level = GlobalAudioHandler.LogMixerStatus ? LogLevel.Debug : LogLevel.Trace;
                    YargLogger.LogFormat(level, "Mixer \"{0}\" created", mixer.Name);
                    _activeMixers.Add(mixer);
                }
            }
        }

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void RemoveMixer(StemMixer mixer)
        {
            lock (_activeMixers)
            {
                var level = GlobalAudioHandler.LogMixerStatus ? LogLevel.Debug : LogLevel.Trace;
                YargLogger.LogFormat(level, "Mixer \"{0}\" disposed", mixer.Name);
                _activeMixers.Remove(mixer);
            }
        }

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    StemMixer[] mixers;
                    lock (_activeMixers)
                    {
                        mixers = _activeMixers.ToArray();
                    }

                    foreach (var mixer in mixers)
                    {
                        mixer.Dispose();
                    }

                    foreach (var sample in SfxSamples)
                    {
                        sample?.Dispose();
                    }

                    foreach (var sample in DrumSfxSamples)
                    {
                        sample?.Dispose();
                    }

                    foreach (var sample in VoxSamples)
                    {
                        sample?.Dispose();
                    }

                    foreach (var sample in MetronomeSamples)
                    {
                        sample?.Dispose();
                    }

                    foreach (var sample in VenueSamples.Values)
                    {
                        sample?.Dispose();
                    }

                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~AudioManager()
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
