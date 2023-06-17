using System;

namespace YARG.Core.Chart
{
    public class DrumNote : Note 
    {
        public int Pad { get; }

        private readonly DrumNoteFlags _drumFlags;

        public bool IsCymbal => (_drumFlags & DrumNoteFlags.Cymbal) != 0;

        public bool IsGhost  => (_drumFlags & DrumNoteFlags.DrumGhost) != 0;
        public bool IsAccent => (_drumFlags & DrumNoteFlags.DrumAccent) != 0;

        public DrumNote(int pad, DrumNoteFlags drumFlags, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0) 
        {
            Pad = pad;
            _drumFlags = drumFlags;
        }
    }

    [Flags]
    public enum DrumNoteFlags
    {
        None = 0,

        Cymbal     = 1 << 0,
        DrumGhost  = 1 << 1,
        DrumAccent = 1 << 2,
    }
}