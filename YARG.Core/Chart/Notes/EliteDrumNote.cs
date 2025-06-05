namespace YARG.Core.Chart
{
    public class EliteDrumNote : Note<EliteDrumNote>
    {
        private DrumNoteFlags _drumFlags;
        public DrumNoteFlags DrumFlags;

        public int Pad { get; }

        private int _padMask;
        public DrumNoteType Dynamics { get; set; }

        public bool IsNeutral => Dynamics == DrumNoteType.Neutral;
        public bool IsAccent => Dynamics == DrumNoteType.Accent;
        public bool IsGhost => Dynamics == DrumNoteType.Ghost;
        public EliteDrumsHatState HatState { get; set; }
        public bool IsOpen => HatState == EliteDrumsHatState.Open;
        public bool IsClosed => HatState == EliteDrumsHatState.Closed;
        public bool IsIndifferent => HatState == EliteDrumsHatState.Indifferent;

        public bool IsFlam { get; set; }

        public float? HitVelocity;

        public bool IsStarPowerActivator => (DrumFlags & DrumNoteFlags.StarPowerActivator) != 0;

        public EliteDrumNote (EliteDrumPad pad, DrumNoteType dynamics, DrumNoteFlags drumFlags, bool isFlam, NoteFlags flags, double time, uint tick)
            : this((int)pad, dynamics, drumFlags, isFlam, flags, time, tick)
        {
        }

        public EliteDrumNote(int pad, DrumNoteType dynamics, DrumNoteFlags drumFlags, bool isFlam, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0)
        {
            Pad = pad;
            Dynamics = dynamics;
            IsFlam = isFlam;
            DrumFlags = _drumFlags = drumFlags;
            _padMask = 1 << pad;
        }

        public EliteDrumNote(EliteDrumNote other) : base(other)
        {
            Pad = other.Pad;
            Dynamics = other.Dynamics;

            DrumFlags = _drumFlags = other._drumFlags;

            _padMask = 1 << other.Pad;
        }

        public override void AddChildNote(EliteDrumNote note)
        {
            if ((_padMask & (1 << note.Pad)) != 0) return;

            _padMask |= note.Pad;

            base.AddChildNote(note);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            DrumFlags = _drumFlags;
            HitVelocity = null;
        }

        public void ActivateFlag(DrumNoteFlags drumNoteFlag)
        {
            _drumFlags |= drumNoteFlag;
            DrumFlags |= drumNoteFlag;
        }

        protected override void CopyFlags(EliteDrumNote other)
        {
            _drumFlags = other._drumFlags;
            DrumFlags = other.DrumFlags;

            Dynamics = other.Dynamics;
        }

        protected override EliteDrumNote CloneNote()
        {
            return new(this);
        }

        public enum EliteDrumPad
        {
            HatPedal,
            Kick,

            Snare,
            HiHat,
            LeftCrash,
            Tom1,
            Tom2,
            Tom3,
            Ride,
            RightCrash,
        }

        public enum EliteDrumsHatState {
            Open,
            Closed,
            Indifferent
        }
    }
}
