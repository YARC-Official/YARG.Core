using System;

namespace YARG.Core.Chart.Events
{

    public class HandMap : ChartEvent, ICloneable<HandMap>
    {
        public enum HandMapType
        {
            Default,
            NoChords,
            AllChords,
            AllBend,
            Solo,
            DropD,
            DropD2,
            ChordC,
            ChordD,
            ChordA
        }

        public HandMapType Type { get; }

        public HandMap(HandMapType type, double time, uint tick) : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public HandMap(HandMap other) : base(other)
        {
            Type = other.Type;
        }

        public HandMap Clone()
        {
            return new HandMap(this);
        }
    }
}