using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public class StemVolume
    {
        private Action<double>? _adjustments;
        private double _volume = AudioHelpers.SONG_VOLUME_MULTIPLIER;

        public event Action<double> Adjustments
        {
            add { _adjustments += value; }
            remove { _adjustments -= value; }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value * AudioHelpers.SONG_VOLUME_MULTIPLIER, 0, AudioHelpers.SONG_VOLUME_MULTIPLIER);
                _adjustments?.Invoke(_volume);
            }
        }
    }
}
