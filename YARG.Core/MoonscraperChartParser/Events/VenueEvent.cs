// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class VenueEvent : Event
    {
        public enum Type
        {
            Lighting,
            PostProcessing,
            Singalong,
            Spotlight,
            Miscellaneous,
        }

        private readonly ID _classID = ID.Venue;
        public override int classID => (int)_classID;

        public Type type;
        public uint length;

        public VenueEvent(Type _type, string _text, uint _position, uint _length = 0) : base(_text, _position)
        {
            type = _type;
            length = _length;
        }

        protected override SongObject SongClone() => Clone();

        public new VenueEvent Clone()
        {
            return new VenueEvent(type, title, tick, length)
            {
                song = song,
            };
        }

        public override string ToString()
        {
            return $"Venue event '{title}' at tick {tick}";
        }
    }
}
