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

    public static void AssertLinkedInListOrder(IReadOnlyList<MoonNote> notes)
    {
        for (int i = 0; i < notes.Count; ++i)
        {
            Assert.That(notes[i].previous, Is.SameAs(i > 0 ? notes[i - 1] : null), $"previous of note {i}");
            Assert.That(notes[i].next, Is.SameAs(i < notes.Count - 1 ? notes[i + 1] : null), $"next of note {i}");
        }
    }
}
