// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class Beat : SyncTrack
    {
        public enum Type
        {
            Measure,
            Beat,
        }

        private readonly ID _classID = ID.Beat;
        public override int classID => (int)_classID;

        public Type type;

        public Beat(uint _position, Type _type) : base(_position)
        {
            type = _type;
        }

        protected override SyncTrack SyncClone() => Clone();

        public new Beat Clone()
        {
            return new Beat(tick, type)
            {
                song = song,
            };
        }

        public override string ToString()
        {
            return $"{type} beat at tick {tick}";
        }
    }
}
