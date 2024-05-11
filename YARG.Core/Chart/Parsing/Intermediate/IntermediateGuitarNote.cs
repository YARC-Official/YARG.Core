using System;

namespace YARG.Core.Chart.Parsing
{
    internal enum IntermediateGuitarFret
    {
        Open,
        Fret1,
        Fret2,
        Fret3,
        Fret4,
        Fret5,
        Fret6,
    }

    [Flags]
    internal enum IntermediateGuitarFlags
    {
        None = 0,

        ForceFlip = 0x01,
        ForceHopo = 0x02,
        ForceStrum = 0x04,

        Tap = 0x08,
    }

    internal class IntermediateGuitarNote : IntermediateEvent
    {
        public IntermediateGuitarFret Fret;
        public IntermediateGuitarFlags Flags;

        public IntermediateGuitarNote(uint tick, uint length, IntermediateGuitarFret fret, IntermediateGuitarFlags flags)
            : base(tick, length)
        {
            Fret = fret;
            Flags = flags;
        }
    }
}