// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using MoonscraperChartEditor.Song;

public static class NoteFunctions {

    public static void ApplyFlagsToChord(this MoonNote moonNote)
    {
        foreach (MoonNote chordNote in moonNote.chord)
        {
            chordNote.flags = CopyChordFlags(chordNote.flags, moonNote.flags);
        }
    }
    
    static MoonNote.Flags CopyChordFlags(MoonNote.Flags original, MoonNote.Flags noteToCopyFrom)
    {
        MoonNote.Flags flagsToPreserve = original & MoonNote.PER_NOTE_FLAGS;
        MoonNote.Flags newFlags = noteToCopyFrom & ~MoonNote.PER_NOTE_FLAGS;
        newFlags |= flagsToPreserve;

        return newFlags;
    }
}
