using System;

namespace YARG.Core.Chart
{
    public class RangeShift : ChartEvent, ICloneable<RangeShift>
    {
        public int Range;
        public int Size;

        public RangeShift(double time, double timeLength, uint tick, uint tickLength, int range, int size)
            : base(time, timeLength, tick, tickLength)
        {
            Range = range;
            Size = size;
        }

        public RangeShift(RangeShift other) : base(other)
        {
            Range = other.Range;
            Size = other.Size;
        }

        public RangeShift Clone()
        {
            return new RangeShift(this);
        }
    }
}