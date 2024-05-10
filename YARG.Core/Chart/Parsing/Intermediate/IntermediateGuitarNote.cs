using System;

namespace YARG.Core.Chart.Parsing
{
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
        public GuitarFret Fret;
        public IntermediateGuitarFlags Flags;

        public IntermediateGuitarNote(uint tick, uint length, GuitarFret fret, IntermediateGuitarFlags flags)
            : base(tick, length)
        {
            Fret = fret;
            Flags = flags;
        }
    }
}