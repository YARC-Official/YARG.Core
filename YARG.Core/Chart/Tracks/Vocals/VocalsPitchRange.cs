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

        // TODO
        // public double ShiftTime { get; }

        public VocalsPitchRange(float minPitch, float maxPitch, // double shiftTime,
            double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            MinimumPitch = minPitch;
            MaximumPitch = maxPitch;

            // ShiftTime = shiftTime;
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