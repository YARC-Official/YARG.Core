// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class TextEvent : SongObject
    {
        public string text;

        public TextEvent(string _title, uint _position)
            : this(ID.Text, _title, _position)
        {
        }

        protected TextEvent(ID id, string _title, uint _position)
            : base(id, _position)
        {
            text = _title;
        }

        protected override bool Equals(SongObject b)
        {
            if (base.Equals(b))
            {
                var realB = (TextEvent) b;
                return realB != null && tick == realB.tick && text == realB.text;
            }

            return false;
        }

        protected override bool LessThan(SongObject b)
        {
            if (classID == b.classID)
            {
                var realB = (TextEvent) b;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (string.Compare(text, realB.text) < 0)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }

        protected override SongObject SongClone() => Clone();

        public new TextEvent Clone()
        {
            return new TextEvent(text, tick);
        }

        public override string ToString()
        {
            return $"Global event at tick {tick} with text '{text}'";
        }
    }
}
