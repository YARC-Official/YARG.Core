using System;

namespace YARG.Core.Audio
{
    public class StemSettings
    {
        public static bool ApplySettings = true;

        private Action<double>? _onVolumeChange;
        private Action<bool>? _onReverbChange;
        private double _volume;
        private bool _reverb;

        public StemSettings()
        {
            _volume = 1;
        }

        public event Action<double> OnVolumeChange
        {
            add { _onVolumeChange += value; }
            remove { _onVolumeChange -= value; }
        }

        public event Action<bool> OnReverbChange
        {
            add { _onReverbChange += value; }
            remove { _onReverbChange -= value; }
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
            set
            {
                if (value != _reverb)
                {
                    _reverb = value;
                    _onReverbChange?.Invoke(value);
                }
            }
        }
    }
}
