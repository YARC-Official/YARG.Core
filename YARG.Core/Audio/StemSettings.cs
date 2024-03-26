using System;

namespace YARG.Core.Audio
{
    public class StemSettings
    {
        private const double BASE = 2;
        private const double FACTOR = BASE - 1;
        private readonly double _volumeScaling;

        private Action<double>? _onVolumeChange;
        private Action<bool>? _onReverbChange;
        private double _volume;
        private double _trueVolume;
        private bool _reverb;

        public StemSettings(double scaling)
        {
            _trueVolume = _volumeScaling = scaling;
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
                double scaled = _volume * _volumeScaling;
                _trueVolume = (Math.Pow(BASE, scaled) - 1) / FACTOR;
                _onVolumeChange?.Invoke(_trueVolume);
            }
        }

        public double TrueVolume => _trueVolume;

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
