using System;
using System.Collections.Generic;

namespace YARG.Core.Audio
{
    public static class MixerAudioHandler
    {
        internal static readonly Dictionary<SongStem, StemSettings> StemSettings;

        static MixerAudioHandler()
        {
            StemSettings = new()
            {
                { SongStem.Song,    new StemSettings() },
                { SongStem.Guitar,  new StemSettings() },
                { SongStem.Bass,    new StemSettings() },
                { SongStem.Rhythm,  new StemSettings() },
                { SongStem.Keys,    new StemSettings() },
                { SongStem.Vocals,  new StemSettings() },
                { SongStem.Drums,   new StemSettings() },
                { SongStem.Crowd,   new StemSettings() },
                { SongStem.Preview, new StemSettings() },
            };
        }

        private static readonly object _instanceLock = new();
        private static StemMixer? _currentMixer;
        public static StemMixer? CurrentMixer
        {
            get
            {
                lock (_instanceLock)
                {
                    return _currentMixer;
                }
            }
        }

        public static double GetTrueVolume(SongStem stem)
        {
            ValidateStem(stem);
            return StemSettings[stem].TrueVolume;
        }

        public static double GetVolumeSetting(SongStem stem)
        {
            ValidateStem(stem);
            return StemSettings[stem].VolumeSetting;
        }

        public static void SetVolumeSetting(SongStem stem, double volume)
        {
            SetVolumeSetting(stem, volume, 0);
        }

        public static void SetVolumeSetting(SongStem stem, double volume, double duration)
        {
            ValidateStem(stem);
            StemSettings[stem].VolumeSetting = volume;
            lock (_instanceLock)
            {
                var trueVolume = GetTrueVolume(stem);
                _currentMixer?[stem]?.SetVolume(trueVolume, duration);
            }
        }

        public static void SetReverbSetting(SongStem stem, bool reverb)
        {
            ValidateStem(stem);
            lock (_instanceLock)
            {
                _currentMixer?[stem]?.SetReverb(reverb);
            }
        }

        public static void SetWhammyPitchSetting(SongStem stem, float percent)
        {
            ValidateStem(stem);
            percent = Math.Clamp(percent, 0, 1);
            lock (_instanceLock)
            {
                _currentMixer?[stem]?.SetWhammyPitch(percent);
            }
        }

        public static void SetMixer(StemMixer mixer)
        {
            lock (_instanceLock)
            {
                _currentMixer = mixer;
            }
        }

        internal static void RemoveMixer(StemMixer mixer)
        {
            lock (_instanceLock)
            {
                if (ReferenceEquals(_currentMixer, mixer))
                {
                    _currentMixer = null;
                }
            }
        }

        private static void ValidateStem(SongStem stem)
        {
            if (!StemSettings.ContainsKey(stem))
            {
                throw new ArgumentException($"Stem {stem} is not a mixer stem", nameof(stem));
            }
        }
    }
}
