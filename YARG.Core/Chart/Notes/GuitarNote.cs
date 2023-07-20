using System;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note<GuitarNote>
    {
        private readonly GuitarNoteFlags _guitarFlags;

        public GuitarNoteFlags GuitarFlags;

        public int Fret     { get; }
        public int NoteMask { get; private set; }

        public uint   SustainTickPosition;
        public double SustainTimeLength;

        public int SustainTickLength;

        public GuitarNoteType Type { get; set; }

        public bool IsStrum => Type == GuitarNoteType.Strum;
        public bool IsHopo  => Type == GuitarNoteType.Hopo;
        public bool IsTap   => Type == GuitarNoteType.Tap;

        public bool IsSustain => TickLength > 0;

        public bool IsExtendedSustain => (GuitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (GuitarFlags & GuitarNoteFlags.Disjoint) != 0;

        public GuitarNote(FiveFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int) fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength)
        {
        }

        public GuitarNote(SixFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int) fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength)
        {
        }

        public GuitarNote(int fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Fret = fret;
            Type = noteType;

            _guitarFlags = guitarFlags;
            GuitarFlags = guitarFlags;

            SustainTickLength = (int) TickLength;

            // Resulting shift is 1 too high, shifting down by 1 corrects this.
            // Reason for not doing (fret - 1) is this breaks open notes.
            // Shifting down by 1 accounts for open notes and sets the mask to 0.
            NoteMask = 1 << fret;
            NoteMask >>= 1;
        }

        public override void AddChildNote(GuitarNote note)
        {
            base.AddChildNote(note);

            NoteMask |= 1 << note.Fret - 1;
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            GuitarFlags = _guitarFlags;
            SustainTickPosition = Tick;

            SustainTimeLength = TimeLength;
            SustainTickLength = (int) TickLength;
        }
    }

    public enum FiveFretGuitarFret
    {
        Open,
        Green,
        Red,
        Yellow,
        Blue,
        Orange,
    }

    public enum SixFretGuitarFret
    {
        Open,
        Black1,
        Black2,
        Black3,
        White1,
        White2,
        White3,
    }

    public enum GuitarNoteType
    {
        Strum,
        Hopo,
        Tap
    }

    [Flags]
    public enum GuitarNoteFlags
    {
        None = 0,

        ExtendedSustain = 1 << 0,
        Disjoint        = 1 << 1,
    }
}