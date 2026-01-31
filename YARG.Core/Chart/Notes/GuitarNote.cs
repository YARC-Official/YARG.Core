using System;
using static YARG.Core.Engine.Keys.FiveLaneKeysEngine;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note<GuitarNote>
    {
        private GuitarNoteFlags _guitarFlags;
        public GuitarNoteFlags GuitarFlags;

        public int Fret         { get; set; }

        // NOTE MASK BIT ASSIGNMENTS
        // INDEX | 5L (Guitar & Keys) | 6F
        // ------|--------------------|-----------
        // 0     | Green              | Black 1
        // 1     | Red                | Black 2
        // 2     | Yellow             | Black 3
        // 3     | Blue               | White 1
        // 4     | Orange             | White 2
        // 5     | (unused)           | White 3
        // 6     | Open               | Open
        // 7-31  | (all unused)       | (all unused)
        public int NoteMask     { get; set; }
        public int DisjointMask { get; set; }

        public GuitarNoteType Type { get; set; }

        public bool IsStrum => Type == GuitarNoteType.Strum;
        public bool IsHopo  => Type == GuitarNoteType.Hopo;
        public bool IsTap   => Type == GuitarNoteType.Tap;

        public bool IsSustain => TickLength > 0;

        public bool IsExtendedSustain => (GuitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (GuitarFlags & GuitarNoteFlags.Disjoint) != 0;

        public FiveLaneKeysAction FiveLaneKeysAction => (FiveFretGuitarFret)Fret switch
        {
            FiveFretGuitarFret.Green => FiveLaneKeysAction.GreenKey,
            FiveFretGuitarFret.Red => FiveLaneKeysAction.RedKey,
            FiveFretGuitarFret.Yellow => FiveLaneKeysAction.YellowKey,
            FiveFretGuitarFret.Blue => FiveLaneKeysAction.BlueKey,
            FiveFretGuitarFret.Orange => FiveLaneKeysAction.OrangeKey,
            FiveFretGuitarFret.Open => FiveLaneKeysAction.OpenNote,
            _ => throw new Exception("Unhandled.")
        };
        public override int LaneNote => NoteMask;

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

            GuitarFlags = _guitarFlags = guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public GuitarNote(GuitarNote other) : base(other)
        {
            Fret = other.Fret;
            Type = other.Type;

            GuitarFlags = _guitarFlags = other._guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public override void AddChildNote(GuitarNote note)
        {
            if ((NoteMask & GetNoteMask(note.Fret)) != 0) return;

            base.AddChildNote(note);

            NoteMask |= GetNoteMask(note.Fret);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            GuitarFlags = _guitarFlags;
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
        Green = 1,
        Red,
        Yellow,
        Blue,
        Orange,
        Open = 7,
    }

    public enum SixFretGuitarFret
    {
        Black1 = 1,
        Black2,
        Black3,
        White1,
        White2,
        White3,
        Open,
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