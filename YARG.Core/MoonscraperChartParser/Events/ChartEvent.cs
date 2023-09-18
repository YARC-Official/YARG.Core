// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class ChartEvent : ChartObject
    {
        private readonly ID _classID = ID.ChartEvent;
        public override int classID => (int)_classID;

        public string eventName { get; private set; }

        public ChartEvent(uint _position, string _eventName) : base(_position)
        {
            eventName = _eventName;
        }

        protected override bool Equals(SongObject b)
        {
            if (b.GetType() == typeof(ChartEvent))
            {
                var realB = (ChartEvent) b;
                return tick == realB.tick && eventName == realB.eventName;
            }
            else
                return base.Equals(b);
        }

        protected override bool LessThan(SongObject b)
        {
            if (b.GetType() == typeof(ChartEvent))
            {
                var realB = (ChartEvent) b;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (string.Compare(eventName, realB.eventName) < 0)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }

        protected override ChartObject ChartClone() => Clone();

        public new ChartEvent Clone()
        {
            return new ChartEvent(tick, eventName)
            {
                song = song,
                chart = chart,
            };
        }

        public override string ToString()
        {
            return $"Local event at tick {tick} with text '{eventName}'";
        }
    }
}
