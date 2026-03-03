using System;

namespace YARG.Core.Audio
{
    public class StemSettings
    {
        public static bool ApplySettings = true;

        private Action<double>? _onVolumeChange;
        private double _volume;
        private bool _reverb;
        private float _whammyPitch;

        public StemSettings()
        {
            _volume = 1;
        }

        public event Action<double> OnVolumeChange
        {
            add { _onVolumeChange += value; }
            remove { _onVolumeChange -= value; }
        }

        public double VolumeSetting
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 1);
                _onVolumeChange?.Invoke(TrueVolume);
            }
        }

        public double TrueVolume => (ApplySettings ? _volume : 1);

        public bool Reverb
        {
            get => _reverb;
            set => _reverb = value;
        }

        public float WhammyPitch
        {
            get => _whammyPitch;
            set => _whammyPitch = Math.Clamp(value, 0, 1);
        }
    }
}
