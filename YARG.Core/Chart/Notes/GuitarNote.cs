using System;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note<GuitarNote>
    {
        private GuitarNoteFlags _guitarFlags;
        public GuitarNoteFlags GuitarFlags;

        public GuitarFret Fret { get; }

        public int DisjointMask { get; }
        public int NoteMask     { get; private set; }

        public uint SustainTicksHeld;

        public GuitarNoteType Type { get; set; }

        public bool IsStrum => Type == GuitarNoteType.Strum;
        public bool IsHopo  => Type == GuitarNoteType.Hopo;
        public bool IsTap   => Type == GuitarNoteType.Tap;

        public bool IsSustain => TickLength > 0;

        public bool IsExtendedSustain => (GuitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (GuitarFlags & GuitarNoteFlags.Disjoint) != 0;

        public GuitarNote(GuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Fret = fret;
            Type = noteType;

            GuitarFlags = _guitarFlags = guitarFlags;

            NoteMask = GetNoteMask((int) Fret);
            DisjointMask = GetNoteMask((int) Fret);
        }

        public GuitarNote(GuitarNote other) : base(other)
        {
            Fret = other.Fret;
            Type = other.Type;

            GuitarFlags = _guitarFlags = other._guitarFlags;

            NoteMask = GetNoteMask((int) Fret);
            DisjointMask = GetNoteMask((int) Fret);
        }

        public override void AddChildNote(GuitarNote note)
        {
            base.AddChildNote(note);

            NoteMask |= GetNoteMask((int) note.Fret);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            GuitarFlags = _guitarFlags;
            SustainTicksHeld = 0;
        }

        protected override void CopyFlags(GuitarNote other)
        {
            _guitarFlags = other._guitarFlags;
            GuitarFlags = other.GuitarFlags;

            Type = other.Type;
        }

        protected override GuitarNote CloneNote()
        {
            return new(this);
        }
    }

    public enum GuitarFret
    {
        Fret1,
        Fret2,
        Fret3,
        Fret4,
        Fret5,
        Fret6,
        Open,

        Green = Fret1,
        Red = Fret2,
        Yellow = Fret3,
        Blue = Fret4,
        Orange = Fret5,

        Black1 = Fret1,
        Black2 = Fret2,
        Black3 = Fret3,
        White1 = Fret4,
        White2 = Fret5,
        White3 = Fret6,
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

    public static class GuitarEnumExtensions
    {
        public static int ToFretIndex(this GuitarFret fret)
        {
            if (fret == GuitarFret.Open)
                throw new ArgumentException($"Opens should not be used as a fret index!");

            return (int) fret;
        }
    }
}