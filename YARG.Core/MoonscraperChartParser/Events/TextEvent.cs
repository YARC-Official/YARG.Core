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

        public override bool ValueEquals(SongObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not TextEvent textEv)
                return baseEq;

            return text == textEv.text;
        }

        public override int InsertionCompareTo(SongObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not TextEvent textEv)
                return baseComp;

            return string.Compare(text, textEv.text);
        }

        protected override SongObject SongClone() => Clone();

        public new TextEvent Clone()
        {
            return new TextEvent(text, tick);
        }

        public override string ToString()
        {
            return $"Text event '{text}' at tick {tick}";
        }
    }
}
