// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class Beat : SongObject
    {
        public enum Type
        {
            Measure,
            Beat,
        }

        public Type type;

        public Beat(uint _position, Type _type)
            : base(ID.Beat, _position)
        {
            type = _type;
        }

        protected override SongObject SongClone() => Clone();

        public new Beat Clone()
        {
            return new Beat(tick, type);
        }

        public override string ToString()
        {
            return $"{type} beat at tick {tick}";
        }
    }
}
