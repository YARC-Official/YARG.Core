using MoonscraperChartEditor.Song;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing;

internal static class MoonNoteAssertions
{
    public static void AssertHasFlag(MoonNote note, MoonNote.Flags flag)
    {
        Assert.That((note.flags & flag) != 0, Is.True, $"Expected {note} to have flag {flag}.");
    }

    public static void AssertDoesNotHaveFlag(MoonNote note, MoonNote.Flags flag)
    {
        Assert.That((note.flags & flag) == 0, Is.True, $"Expected {note} not to have flag {flag}.");
    }
}
