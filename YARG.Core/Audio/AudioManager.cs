using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public enum StarPowerFxMode
    {
        Off,
        MultitrackOnly,
        On
    }

    public abstract class AudioManager : IDisposable
    {
        public const int WHAMMY_FFT_DEFAULT = 2048;
        public const int WHAMMY_OVERSAMPLE_DEFAULT = 8;

        public const double MINIMUM_STEM_VOLUME = 0.15;

        private static AudioManager? _instance;

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


        protected static SampleChannel[] _sfxSamples = new SampleChannel[AudioHelpers.SfxPaths.Count];
        public static void PlaySoundEffect(SfxSample sample)
        {
            _sfxSamples[(int) sample]?.Play();
        }

        protected internal static readonly Dictionary<SongStem, StemSettings> StemSettings;

        static AudioManager()
        {
            var vocals = new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER);
            var drums = new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER);

            StemSettings = new()
            {
                { SongStem.Song,    new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Guitar,  new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Bass,    new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Rhythm,  new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Keys,    new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Vocals,  vocals },
                { SongStem.Vocals1, vocals },
                { SongStem.Vocals2, vocals },
                { SongStem.Drums,   drums },
                { SongStem.Drums1,  drums },
                { SongStem.Drums2,  drums },
                { SongStem.Drums3,  drums },
                { SongStem.Drums4,  drums },
                { SongStem.Crowd,   new StemSettings(AudioHelpers.SONG_VOLUME_MULTIPLIER) },
                { SongStem.Sfx,     new StemSettings(1) },
                { SongStem.Preview, new StemSettings(1) },
                { SongStem.Master,  new StemSettings(1) },
            };
        }

        public static double GetVolumeSetting(SongStem stem)
        {
            return StemSettings[stem].Volume;
        }

        public static void SetVolumeSetting(SongStem stem, double volume)
        {
            StemSettings[stem].Volume = volume;
        }

        public static bool GetReverbSetting(SongStem stem)
        {
            return StemSettings[stem].Reverb;
        }

        public static void SetReverbSetting(SongStem stem, bool reverb)
        {
            StemSettings[stem].Reverb = reverb;
        }

        public static bool UseWhammyFx;
        public static bool IsChipmunkSpeedup;

        public static bool UseMinimumStemVolume;

        /// <summary>
        /// The number of semitones to bend the pitch by. Must be at least 1;
        /// </summary>
        public static float WhammyPitchShiftAmount = 1f;

        // Not implemented, as changing the FFT size causes BASS_FX to crash
        // /// <summary>
        // /// The size of the whammy FFT buffer. Must be a power of 2, up to 8192.
        // /// </summary>
        // /// <remarks>
        // /// Changes to this value will not be applied until the next song plays.
        // /// </remarks>
        // public int WhammyFFTSize
        // {
        //     get => (int)Math.Pow(2, _whammyFFTSize);
        //     set => _whammyFFTSize = (int)Math.Log(value, 2);
        // }
        // private int _whammyFFTSize = WHAMMY_FFT_DEFAULT;

        /// <summary>
        /// The oversampling factor of the whammy SFX. Must be at least 4.
        /// </summary>
        /// <remarks>
        /// Changes to this value will not be applied until the next song plays.
        /// </remarks>
        public static int WhammyOversampleFactor = WHAMMY_OVERSAMPLE_DEFAULT;

        private bool _disposed;
        private List<StemMixer> _activeMixers = new();

        public double PlaybackBufferLength { get; protected set; }

        public abstract ReadOnlySpan<string> SupportedFormats { get; }

        protected AudioManager()
        {
            StemSettings[SongStem.Master].OnVolumeChange += SetMasterVolume;
        }

        public abstract StemMixer? CreateMixer(string name, float speed);

        public abstract StemMixer? CreateMixer(string name, Stream stream, float speed);

        public StemMixer? LoadCustomFile(string name, Stream stream, float speed, SongStem stem = SongStem.Song)
        {
            YargLogger.LogInfo("Loading custom audio file");
            var mixer = CreateMixer(name, stream, speed);
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
            var mixer = LoadCustomFile(file, stream, speed, stem);
            if (mixer == null)
            {
                YargLogger.LogFormatError("Failed to load audio file{0}!", file);
                stream.Dispose();
                return null;
            }
            return mixer;
        }

        public abstract MicDevice? GetInputDevice(string name);

        public abstract List<(int id, string name)> GetAllInputDevices();

        public abstract MicDevice? CreateDevice(int deviceId, string name);

        protected abstract void SetMasterVolume(double volume);

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void AddMixer(StemMixer mixer)
        {
            lock (_activeMixers)
            {
                YargLogger.LogFormatInfo("Mixer \"{0}\" created", mixer.Name);
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
                YargLogger.LogFormatInfo($"Mixer \"{0}\" disposed", mixer.Name);
                _activeMixers.Remove(mixer);
            }
        }

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
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

                foreach (var sample in _sfxSamples)
                {
                    sample?.Dispose();
                }

                StemSettings[SongStem.Master].OnVolumeChange -= SetMasterVolume;
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
