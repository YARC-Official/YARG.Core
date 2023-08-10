using System;

namespace YARG.Core.Chart
{
    public class TimeSignatureChange : SyncEvent, ICloneable<TimeSignatureChange>
    {
        public uint Numerator   { get; }
        public uint Denominator { get; }

        public TimeSignatureChange(uint numerator, uint denominator, double time, uint tick) : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public TimeSignatureChange Clone()
        {
            return new(Numerator, Denominator, Time, Tick);
        }
    }
}