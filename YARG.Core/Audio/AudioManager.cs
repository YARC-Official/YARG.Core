using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public abstract class AudioManager : IDisposable
    {
        public static readonly AudioOptions Options = new();

        private static AudioManager? _instance;
        protected static double[] _stemVolumes = new double[AudioHelpers.SupportedStems.Count];
        protected static SampleChannel[] _sfxSamples = new SampleChannel[AudioHelpers.SfxPaths.Count];

        public static double MasterVolume { get; protected set; } = 1;
        public static double SfxVolume { get; protected set; } = 1;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("Audio manager not initialized");
                }
                return _instance;
            }
        }

        public static AudioManager Initialize<TAudioManager>()
            where TAudioManager : AudioManager, new()
        {
            if (_instance is not TAudioManager)
            {
                _instance?.Dispose();
            }
            return _instance = new TAudioManager();
        }

        public static void Close()
        {
            _instance?.Dispose();
        }

        public static void PlaySoundEffect(SfxSample sample)
        {
            _sfxSamples[(int) sample]?.Play();
        }

        public static double GetVolumeSetting(SongStem stem)
        {
            return stem switch
            {
                SongStem.Master or
                SongStem.Preview => MasterVolume,
                SongStem.Sfx => SfxVolume,
                _ => _stemVolumes[(int) stem]
            };
        }

        private bool _disposed;
        private List<StemMixer> _activeMixers = new();

        public double PlaybackBufferLength { get; protected set; }

        public abstract ReadOnlySpan<string> SupportedFormats { get; }

        public abstract StemMixer? CreateMixer(float speed);

        public abstract StemMixer? CreateMixer(Stream stream, float speed);

        public StemMixer? LoadCustomFile(Stream stream, float speed, SongStem stem = SongStem.Song)
        {
            YargLogger.LogInfo("Loading custom audio file");
            var mixer = CreateMixer(stream, speed);
            if (mixer == null)
            {
                return null;
            }

            if (!mixer.AddChannel(SongStem.Song))
            {
                mixer.Dispose();
                return null;
            }
            YargLogger.LogInfo("Custom audio file loaded");
            return mixer;
        }

        public StemMixer? LoadCustomFile(string file, float speed, SongStem stem = SongStem.Song)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            var mixer = LoadCustomFile(stream, speed, stem);
            if (mixer == null)
            {
                YargLogger.LogFormatError("Failed to load audio file{0}!", file);
                stream.Dispose();
                return null;
            }
            return mixer;
        }

        public abstract void UpdateVolumeSetting(SongStem stem, double volume);

        public abstract MicDevice? GetInputDevice(string name);

        public abstract List<(int id, string name)> GetAllInputDevices();

        public abstract MicDevice? CreateDevice(int deviceId, string name);

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void AddMixer(StemMixer mixer)
        {
            lock (_activeMixers)
            {
                _activeMixers.Add(mixer);
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
                _activeMixers.Remove(mixer);
            }
        }

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                lock (_activeMixers)
                {
                    foreach (var mixer in _activeMixers)
                    {
                        mixer.Dispose();
                    }
                }

                foreach (var sample in _sfxSamples)
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
