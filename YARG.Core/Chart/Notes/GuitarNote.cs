using System;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note<GuitarNote>
    {
        private GuitarNoteFlags _guitarFlags;
        public GuitarNoteFlags GuitarFlags;

        private int _fret;

        public int Fret
        {
            get => _fret;
            set
            {
                if (value == _fret)
                    return;

                int mask = GetNoteMask(value);
                int oldMask = GetNoteMask(_fret);

                // If we're a child note, adjust our parent's mask to reflect the change
                if (Parent != null)
                {
                    if ((Parent.NoteMask & mask) != 0)
                        throw new InvalidOperationException($"Fret {value} already exists in the current chord!");

                    Parent.NoteMask &= ~oldMask;
                    Parent.NoteMask |= mask;

                    NoteMask = mask;
                }
                // Otherwise, adjust our own mask
                else
                {
                    if ((NoteMask & mask) != 0)
                        throw new InvalidOperationException($"Fret {value} already exists in the current chord!");

                    NoteMask &= ~oldMask;
                    NoteMask |= mask;
                }

                _fret = value;
                DisjointMask = mask;
            }
        }

        public int DisjointMask { get; private set; }
        public int NoteMask     { get; private set; }

        public uint SustainTicksHeld;

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
            _fret = fret;
            Type = noteType;

            GuitarFlags = _guitarFlags = guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public GuitarNote(GuitarNote other) : base(other)
        {
            _fret = other._fret;
            Type = other.Type;

            GuitarFlags = _guitarFlags = other._guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public override void AddChildNote(GuitarNote note)
        {
            base.AddChildNote(note);

            NoteMask |= GetNoteMask(note.Fret);
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