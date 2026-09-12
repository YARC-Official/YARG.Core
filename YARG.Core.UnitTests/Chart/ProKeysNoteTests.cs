using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Chart;

public class ProKeysNoteTests
{
    [Test]
    public void PrimaryCtor_SetsDisjointMask()
    {
        var note = new ProKeysNote(7, ProKeysNoteFlags.None, NoteFlags.None, 0, 0, 0, 0);
        Assert.That(note.DisjointMask, Is.EqualTo(1 << 7));
    }

    [Test]
    public void CopyCtor_SetsDisjointMask()
    {
        var note = new ProKeysNote(12, ProKeysNoteFlags.None, NoteFlags.None, 0, 0, 0, 0);
        var copy = note.Clone();
        Assert.That(copy.DisjointMask, Is.EqualTo(1 << 12));
    }

    [Test]
    public void PrimaryAndCopyCtor_DisjointMaskAgree()
    {
        var primary = new ProKeysNote(3, ProKeysNoteFlags.None, NoteFlags.None, 0, 0, 0, 0);
        Assert.That(primary.Clone().DisjointMask, Is.EqualTo(primary.DisjointMask));
    }
}
