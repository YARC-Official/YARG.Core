using System;

namespace YARG.Core.Chart
{
    public class DrumNote : Note 
    {
        private readonly DrumNoteFlags _drumFlags;

        public int Pad { get; }

        public DrumNoteType Type { get; }

        public bool IsStarPowerActivator => (_drumFlags & DrumNoteFlags.StarPowerActivator) != 0;

        public DrumNote(FourLaneDrumPad pad, DrumNoteType noteType, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, noteType, drumFlags, flags, time, tick) 
        {
        }

        public DrumNote(FiveLaneDrumPad pad, DrumNoteType noteType, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, noteType, drumFlags, flags, time, tick) 
        {
        }

        public DrumNote(int pad, DrumNoteType noteType, DrumNoteFlags drumFlags, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0) 
        {
            Pad = pad;
            Type = noteType;
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

    public enum DrumNoteType
    {
        Neutral,
        Ghost,
        Accent,
    }

    [Flags]
    public enum DrumNoteFlags
    {
        None = 0,

        StarPowerActivator = 1 << 0,
    }
}