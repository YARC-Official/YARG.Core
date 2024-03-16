using System;

namespace YARG.Core.Audio
{
    public class StemSettings
    {
        private readonly double _volumeScaling;

        private Action<double>? _onVolumeChange;
        private Action<bool>? _onReverbChange;
        private double _volume;
        private bool _reverb;

        public StemSettings(double factor)
        {
            _volume = _volumeScaling = factor;
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
                _volume = Math.Clamp(value * _volumeScaling, 0, _volumeScaling);
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
