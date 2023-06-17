using System;

namespace YARG.Core.Chart
{
    public class DrumNote : Note 
    {
        private readonly DrumNoteFlags _drumFlags;

        public int Pad { get; }

        public bool IsGhost  => (_drumFlags & DrumNoteFlags.DrumGhost) != 0;
        public bool IsAccent => (_drumFlags & DrumNoteFlags.DrumAccent) != 0;

        public DrumNote(FourLaneDrumPad pad, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, drumFlags, flags, time, tick) 
        {
        }

        public DrumNote(FiveLaneDrumPad pad, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, drumFlags, flags, time, tick) 
        {
        }

        public DrumNote(int pad, DrumNoteFlags drumFlags, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0) 
        {
            Pad = pad;
            _drumFlags = drumFlags;
        }
    }

    public enum FourLaneDrumPad
    {
        Kick,

        RedDrum,
        YellowDrum,
        BlueDrum,
        GreenDrum,

        YellowCymbal,
        BlueCymbal,
        GreenCymbal,
    }

    public enum FiveLaneDrumPad
    {
        Kick,

        Red,
        Yellow,
        Blue,
        Orange,
        Green,
    }

    [Flags]
    public enum DrumNoteFlags
    {
        None = 0,

        DrumGhost  = 1 << 1,
        DrumAccent = 1 << 2,
    }
}