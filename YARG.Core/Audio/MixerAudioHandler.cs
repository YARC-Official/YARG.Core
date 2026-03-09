namespace YARG.Core.Audio
{
    public static class MixerAudioHandler
    {
        private static readonly object _instanceLock = new();
        private static IStemController? _currentController;

        public static void SetVolumeSetting(SongStem stem, double volume, double duration = 0)
        {
            lock (_instanceLock)
            {
                _currentController?.SetVolume(stem, volume, duration);
            }
        }

        public static void SetReverbSetting(SongStem stem, bool reverb)
        {
            lock (_instanceLock)
            {
                _currentController?.SetReverb(stem, reverb);
            }
        }

        public static void SetWhammyPitchSetting(SongStem stem, float percent)
        {
            lock (_instanceLock)
            {
                _currentController?.SetWhammyPitch(stem, percent);
            }
        }

        public static void SetMixer(IStemController controller)
        {
            lock (_instanceLock)
            {
                _currentController = controller;
            }
        }

        internal static void RemoveMixer(StemMixer mixer)
        {
            lock (_instanceLock)
            {
                if (object.ReferenceEquals(_currentController, mixer))
                {
                    _currentController = null;
                }
            }
        }
    }
}
