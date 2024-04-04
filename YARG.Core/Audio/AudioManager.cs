using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public abstract class AudioManager
    {
        private bool _disposed;
        private static List<StemMixer> _activeMixers = new();
        protected internal readonly SampleChannel[] SfxSamples = new SampleChannel[AudioHelpers.SfxPaths.Count];
        protected internal double PlaybackBufferLength;

        protected internal abstract ReadOnlySpan<string> SupportedFormats { get; }

        internal StemMixer? LoadCustomFile(string name, Stream stream, float speed, SongStem stem = SongStem.Song)
        {
            YargLogger.LogInfo("Loading custom audio file");
            var mixer = CreateMixer(name, stream, speed, false);
            if (mixer == null)
            {
                return null;
            }

            if (!mixer.AddChannel(stem))
            {
                mixer.Dispose();
                return null;
            }
            YargLogger.LogInfo("Custom audio file loaded");
            return mixer;
        }

        internal StemMixer? LoadCustomFile(string file, float speed, SongStem stem = SongStem.Song)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            var mixer = LoadCustomFile(file, stream, speed, stem);
            if (mixer == null)
            {
                YargLogger.LogFormatError("Failed to load audio file{0}!", file);
                stream.Dispose();
                return null;
            }
            return mixer;
        }

        protected internal abstract StemMixer? CreateMixer(string name, float speed, bool clampStemVolume);

        protected internal abstract StemMixer? CreateMixer(string name, Stream stream, float speed, bool clampStemVolume);

        protected internal abstract MicDevice? GetInputDevice(string name);

        protected internal abstract List<(int id, string name)> GetAllInputDevices();

        protected internal abstract MicDevice? CreateDevice(int deviceId, string name);

        protected internal abstract void SetMasterVolume(double volume);

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
                    YargLogger.LogFormatInfo("Mixer \"{0}\" created", mixer.Name);
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
                YargLogger.LogFormatInfo("Mixer \"{0}\" disposed", mixer.Name);
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
