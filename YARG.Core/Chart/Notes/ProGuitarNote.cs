﻿using System;

namespace YARG.Core.Chart
{
    public class ProGuitarNote : Note
    {
        private readonly ProGuitarNoteFlags _proFlags;

        public int String   { get; }
        public int Fret     { get; }

        public ProGuitarNoteType Type { get; set; }

        public bool IsStrum => Type == ProGuitarNoteType.Strum;
        public bool IsHopo  => Type == ProGuitarNoteType.Hopo;
        public bool IsTap   => Type == ProGuitarNoteType.Tap;

        public bool IsSustain => TickLength > 0;

        public bool IsExtendedSustain => (_proFlags & ProGuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (_proFlags & ProGuitarNoteFlags.Disjoint) != 0;

        public bool IsMuted => (_proFlags & ProGuitarNoteFlags.Muted) != 0;

        public ProGuitarNote(int stringNo, int fret, ProGuitarNoteType type, ProGuitarNoteFlags proFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            String = stringNo;
            Fret = fret;
            Type = type;

            _proFlags = proFlags;
        }
    }

    public enum ProGuitarNoteType
    {
        Strum,
        Hopo,
        Tap,
    }

    [Flags]
    public enum ProGuitarNoteFlags
    {
        None = 0,

        ExtendedSustain = 1 << 0,
        Disjoint        = 1 << 1,

        Muted = 1 << 2,
    }
}