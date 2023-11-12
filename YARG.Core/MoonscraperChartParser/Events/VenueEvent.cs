// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class VenueEvent : Event
    {
        private readonly ID _classID = ID.Venue;
        public override int classID => (int)_classID;

        public VenueLookup.Type type;
        public uint length;

        public VenueEvent(VenueLookup.Type _type, string _text, uint _position, uint _length = 0) : base(_text, _position)
        {
            type = _type;
            length = _length;
        }

        protected override SongObject SongClone() => Clone();

        public new VenueEvent Clone()
        {
            return new VenueEvent(type, title, tick, length);
        }

        public override string ToString()
        {
            return $"Venue event '{title}' at tick {tick}";
        }
    }
}
