using System;
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonAnimation : MoonText
    {
        // public int  noteNumber;
        public AnimationLookup.Type type;
        public uint length;

        // public MoonAnimation(int _noteNumber, uint _position, uint _length)
        //     : base(ID.Animation, _position)
        // {
        //     // Translate note number to text
        //     noteNumber = _noteNumber;
        //     length = _length;
        // }

        public MoonAnimation(AnimationLookup.Type _type, string _text, uint _position, uint _length = 0) : base(ID.Animation, _text, _position)
        {
            type = _type;
            length = _length;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonAnimation animationEv)
                return baseEq;

            return type == animationEv.type && length == animationEv.length;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonAnimation animationEv)
                return baseComp;

            return ((int) type).CompareTo((int) animationEv.type);
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonAnimation Clone()
        {
            return new MoonAnimation(type, text, tick, length);
        }

        public override string ToString()
        {
            return $"Animation event '{text}' at tick {tick}, length {length}";
        }
    }
}