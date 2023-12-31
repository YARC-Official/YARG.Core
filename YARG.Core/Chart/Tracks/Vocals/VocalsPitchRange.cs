using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A range shift on the vocals track.
    /// </summary>
    public class VocalsPitchRange : ChartEvent, ICloneable<VocalsPitchRange>
    {
        public float MinimumPitch { get; }
        public float MaximumPitch { get; }

        public VocalsPitchRange(float minPitch, float maxPitch,
            double time, uint tick)
            : base(time, 0, tick, 0)
        {
            MinimumPitch = minPitch;
            MaximumPitch = maxPitch;
        }

        public VocalsPitchRange(ChartEvent other) : base(other)
        {
        }

        public VocalsPitchRange Clone()
        {
            return new(this);
        }
    }
}