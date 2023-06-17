using System;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note
    {
        private readonly GuitarNoteFlags _guitarFlags;

        public int Fret     { get; }
        public int NoteMask { get; private set; }

        public GuitarNoteType Type { get; }

        public bool IsStrum => Type == GuitarNoteType.Strum;
        public bool IsHopo  => Type == GuitarNoteType.Hopo;
        public bool IsTap   => Type == GuitarNoteType.Tap;

        public bool IsSustain { get; }

        public bool IsExtendedSustain => (_guitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (_guitarFlags & GuitarNoteFlags.Disjoint) != 0;

        public GuitarNote(FiveFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int)fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength) 
        {
        }

        public GuitarNote(SixFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int)fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength) 
        {
        }

        public GuitarNote(int fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength) 
        {
            Fret = fret;

            IsSustain = tickLength > 0;

            Type = noteType;

            _guitarFlags = guitarFlags;

            NoteMask = 1 << fret - 1;
        }

        public override void AddChildNote(Note note)
        {
            if (note is not GuitarNote guitarNote)
                return;

            base.AddChildNote(note);

            NoteMask |= 1 << guitarNote.Fret;
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