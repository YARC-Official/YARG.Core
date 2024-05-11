using System;

namespace YARG.Core.Chart.Parsing
{
    internal enum IntermediateDrumPad
    {
        Kick,
        Lane1,
        Lane2,
        Lane3,
        Lane4,
        Lane5,
    }

    [Flags]
    internal enum IntermediateDrumsNoteFlags
    {
        None = 0,

        Cymbal = 0x01,
        Accent = 0x02,
        Ghost = 0x04,

        DiscoFlip = 0x08,
        ExpertPlus = 0x10,
    }

    internal class IntermediateDrumsNote : IntermediateEvent
    {
        public IntermediateDrumPad Pad;
        public IntermediateDrumsNoteFlags Flags;

        public IntermediateDrumsNote(uint tick, uint length, IntermediateDrumPad pad, IntermediateDrumsNoteFlags flags)
            : base(tick, length)
        {
            Pad = pad;
            Flags = flags;
        }
    }
}