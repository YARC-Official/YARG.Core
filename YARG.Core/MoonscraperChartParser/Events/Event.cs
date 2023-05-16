// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    public class Event : SongObject
    {
        private readonly ID _classID = ID.Event;
        public override int classID => (int)_classID;

        public string title { get; private set; }

        public Event(string _title, uint _position) : base(_position)
        {
            title = _title;
        }

        protected override bool Equals(SongObject b)
        {
            if (base.Equals(b))
            {
                var realB = b as Event;
                return realB != null && tick == realB.tick && title == realB.title;
            }

            return false;
        }

        protected override bool LessThan(SongObject b)
        {
            if (classID == b.classID)
            {
                var realB = b as Event;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (string.Compare(title, realB.title) < 0)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }
    }
}
