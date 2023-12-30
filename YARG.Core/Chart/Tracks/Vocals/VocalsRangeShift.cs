using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A range shift on the vocals track.
    /// </summary>
    public class VocalsRangeShift : ChartEvent, ICloneable<VocalsRangeShift>
    {
        public VocalsRangeShift(double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
        }

        public VocalsRangeShift(ChartEvent other) : base(other)
        {
        }

        public VocalsRangeShift Clone()
        {
            return new(this);
        }
    }
}