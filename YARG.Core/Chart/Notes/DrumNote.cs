using System;

namespace YARG.Core.Chart
{
    public class DrumNote : Note<DrumNote>
    {
        private DrumNoteFlags _drumFlags;
        public DrumNoteFlags DrumFlags;

        public int Pad { get; }

        public DrumNoteType Type { get; set; }

        public bool IsNeutral => Type == DrumNoteType.Neutral;
        public bool IsAccent  => Type == DrumNoteType.Accent;
        public bool IsGhost   => Type == DrumNoteType.Ghost;

        public float? HitVelocity;
        public bool AwardVelocityBonus;

        public bool IsStarPowerActivator => (DrumFlags & DrumNoteFlags.StarPowerActivator) != 0;

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

            DrumFlags = _drumFlags = drumFlags;
        }

        public DrumNote(DrumNote other) : base(other)
        {
            Pad = other.Pad;
            Type = other.Type;

            DrumFlags = _drumFlags = other._drumFlags;
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            DrumFlags = _drumFlags;
            HitVelocity = null;
            AwardVelocityBonus = false;
        }

        protected override void CopyFlags(DrumNote other)
        {
            _drumFlags = other._drumFlags;
            DrumFlags = other.DrumFlags;

            Type = other.Type;
        }

        protected override DrumNote CloneNote()
        {
            return new(this);
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