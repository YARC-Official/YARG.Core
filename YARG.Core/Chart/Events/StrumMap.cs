using System;

namespace YARG.Core.Chart.Events
{
    public class StrumMap : ChartEvent, ICloneable<StrumMap>
    {
        public enum StrumMapType
        {
            Default,
            Pick,
            SlapBass
        }

        public StrumMapType Type { get; }

        public StrumMap(StrumMapType type, double time, uint tick) : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public StrumMap(StrumMap other) : base(other)
        {
            Type = other.Type;
        }

        public StrumMap Clone()
        {
            return new StrumMap(this);
        }
    }
}