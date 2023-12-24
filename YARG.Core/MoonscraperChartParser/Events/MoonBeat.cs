// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonBeat : MoonObject
    {
        public enum Type
        {
            Measure,
            Beat,
        }

        public Type type;

        public MoonBeat(uint _position, Type _type)
            : base(ID.Beat, _position)
        {
            type = _type;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonBeat beat)
                return baseEq;

            return type == beat.type;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonBeat Clone()
        {
            return new MoonBeat(tick, type);
        }

        public override string ToString()
        {
            return $"{type} line at tick {tick}";
        }
    }
}
