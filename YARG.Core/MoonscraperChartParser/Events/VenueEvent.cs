// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class VenueEvent : Event
    {
        private readonly ID _classID = ID.Venue;
        public override int classID => (int)_classID;

        public uint length;

        public VenueEvent(string _text, uint _position, uint _length = 0) : base(_text, _position)
        {
            length = _length;
        }

        protected override SongObject SongClone() => Clone();

        public new VenueEvent Clone()
        {
            return new VenueEvent(title, tick, length)
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
