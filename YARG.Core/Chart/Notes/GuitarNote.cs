using System;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note
    {
        public int Fret     { get; }
        public int NoteMask { get; private set; }

        private readonly GuitarNoteFlags _guitarFlags;

        public bool IsSustain { get; }

        public bool IsExtendedSustain => (_guitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (_guitarFlags & GuitarNoteFlags.Disjoint) != 0;

        private bool _isForced;

        private bool _isStrum;
        private bool _isHopo;
        private bool _isTap;

        public bool IsStrum
        {
            get => _isStrum;
            set
            {
                if (value)
                {
                    IsHopo = false;
                    IsTap = false;
                }

                _isStrum = true;
            }
        }

        public bool IsHopo
        {
            get => _isHopo;
            set
            {
                if (value)
                {
                    IsStrum = false;
                    IsTap = false;
                }

                _isHopo = true;
            }
        }

        public bool IsTap
        {
            get => _isTap;
            set
            {
                if (value)
                {
                    IsStrum = false;
                    IsHopo = false;
                }

                _isTap = true;
            }
        }

        public GuitarNote(int fret, MoonNote.MoonNoteType moonNoteType, GuitarNoteFlags guitarFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength) 
        {
            Fret = fret;

            IsSustain = tickLength > 0;

            _isStrum = moonNoteType == MoonNote.MoonNoteType.Strum;
            _isTap = moonNoteType == MoonNote.MoonNoteType.Tap;
            _isHopo = moonNoteType == MoonNote.MoonNoteType.Hopo && !_isTap;

            _guitarFlags = guitarFlags;

            NoteMask = 1 << fret - 1;
        }

        public override void AddChildNote(Note note)
        {
            if (note is not GuitarNote guitarNote)
                return;

            base.AddChildNote(note);

            NoteMask |= 1 << guitarNote.Fret - 1;
        }
    }

    [Flags]
    public enum GuitarNoteFlags
    {
        None = 0,

        ExtendedSustain = 1 << 0,
        Disjoint        = 1 << 1,
    }
}