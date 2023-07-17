// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal abstract class SongObject
    {
        /// <summary>
        /// The song this object is connected to.
        /// </summary>
        [NonSerialized]
        public MoonSong song;
        /// <summary>
        /// The tick position of the object
        /// </summary>
        public uint tick;

        public abstract int classID { get; }

        public SongObject(uint _tick)
        {
            tick = _tick;
        }

        /// <summary>
        /// Automatically converts the object's tick position into the time it will appear in the song.
        /// </summary>
        public double time => song.TickToTime(tick, song.resolution);

        // Clone needs to be hideable so it can return a different type in derived classes
        protected abstract SongObject SongClone();
        public SongObject Clone() => SongClone();

        public static bool operator ==(SongObject a, SongObject b)
        {
            bool aIsNull = a is null;
            bool bIsNull = b is null;

            if (aIsNull || bIsNull)
                return aIsNull == bIsNull;
            else
                return a.Equals(b);
        }

        protected virtual bool Equals(SongObject b)
        {
            return tick == b.tick && classID == b.classID;
        }

        public static bool operator !=(SongObject a, SongObject b)
        {
            return !(a == b);
        }

        protected virtual bool LessThan(SongObject b)
        {
            return tick < b.tick || (tick == b.tick && classID < b.classID);
        }

        public static bool operator <(SongObject a, SongObject b)
        {
            return a.LessThan(b);
        }

        public static bool operator >(SongObject a, SongObject b)
        {
            if (a != b)
                return !(a < b);
            else
                return false;
        }

        public static bool operator <=(SongObject a, SongObject b)
        {
            return a < b || a == b;
        }

        public static bool operator >=(SongObject a, SongObject b)
        {
            return a > b || a == b;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return $"{classID} at tick {tick}";
        }

        /// <summary>
        /// Allows different classes to be sorted and grouped together in arrays by giving each class a comparable numeric value that is greater or less than other classes.
        /// </summary>
        public enum ID
        {
            TimeSignature,
            BPM,
            Anchor,
            Beat,
            Event,
            Section,
            Note,
            Special,
            ChartEvent,
        }
    }
}
