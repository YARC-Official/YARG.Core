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
        private bool _reverb;

        public StemSettings(double scaling)
        {
            _volumeScaling = scaling;
            _volume = Math.Log(scaling * FACTOR + 1, BASE);
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

        public double Volume
        {
            get => _volume;
            set
            {
                double scaled = Math.Clamp(value * _volumeScaling, 0, _volumeScaling);
                _volume = Math.Log(scaled * FACTOR + 1, BASE);
                _onVolumeChange?.Invoke(_volume);
            }
        }

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
