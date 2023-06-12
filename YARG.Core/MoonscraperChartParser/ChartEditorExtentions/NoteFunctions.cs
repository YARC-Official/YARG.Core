// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using MoonscraperChartEditor.Song;

public static class NoteFunctions
{
    public static void ApplyFlagsToChord(this MoonNote note)
    {
        foreach (var chordNote in note.chord)
        {
            chordNote.flags = CopyChordFlags(chordNote.flags, note.flags);
        }
    }

    private static MoonNote.Flags CopyChordFlags(MoonNote.Flags original, MoonNote.Flags noteToCopyFrom)
    {
        var flagsToPreserve = original & MoonNote.PER_NOTE_FLAGS;
        var newFlags = noteToCopyFrom & ~MoonNote.PER_NOTE_FLAGS;
        newFlags |= flagsToPreserve;

        return newFlags;
    }
}
