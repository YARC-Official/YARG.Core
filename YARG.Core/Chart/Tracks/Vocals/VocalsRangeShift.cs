using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A range shift on the vocals track.
    /// </summary>
    public class VocalsRangeShift : ChartEvent, ICloneable<VocalsRangeShift>
    {
        public float MinimumPitch { get; }
        public float MaximumPitch { get; }

        public double ShiftLength { get; }

        public VocalsRangeShift(float minPitch, float maxPitch, double shiftLength,
            double time, uint tick)
            : base(time, 0, tick, 0)
        {
            MinimumPitch = minPitch;
            MaximumPitch = maxPitch;

            ShiftLength = shiftLength;
        }

        public VocalsRangeShift(VocalsRangeShift other)
            : base(other)
        {
            MinimumPitch = other.MinimumPitch;
            MaximumPitch = other.MaximumPitch;

            ShiftLength = other.ShiftLength;
        }

        public VocalsRangeShift Clone()
        {
            return new(this);
        }
    }
}