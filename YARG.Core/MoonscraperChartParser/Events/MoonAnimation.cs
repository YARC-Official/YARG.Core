using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonAnimation : MoonObject
    {
        public int  noteNumber;
        public uint length;

        public MoonAnimation(int _noteNumber, uint _position, uint _length)
            : base(ID.Animation, _position)
        {
            noteNumber = _noteNumber;
            length = _length;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonAnimation animationEv)
                return baseEq;

            return noteNumber == animationEv.noteNumber;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonAnimation animationEv)
                return baseComp;

            if (noteNumber == animationEv.noteNumber)
                return 0;
            return noteNumber < animationEv.noteNumber ? -1 : 1;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonAnimation Clone()
        {
            return new MoonAnimation(noteNumber, tick, length);
        }

        public override string ToString()
        {
            return $"Animation event '{noteNumber}' at tick {tick}, length {length}";
        }
    }
}