using System;

namespace YARG.Core.Audio
{
    public static class MixerAudioHandler
    {
        private static readonly object _instanceLock = new();
        private static StemMixer? _currentMixer;

        public static void SetVolumeSetting(SongStem stem, double volume, double duration = 0)
        {
            ValidateStem(stem);
            volume = Math.Clamp(volume, 0, 1);
            lock (_instanceLock)
            {
                _currentMixer?[stem]?.SetVolume(volume, duration);
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
            var isValidStem = stem is
                SongStem.Song or
                SongStem.Guitar or
                SongStem.Bass or
                SongStem.Rhythm or
                SongStem.Keys or
                SongStem.Vocals or
                SongStem.Drums or
                SongStem.Crowd or
                SongStem.Preview;

            if (!isValidStem)
            {
                throw new ArgumentException($"Stem {stem} is not a mixer stem", nameof(stem));
            }
        }
    }
}
