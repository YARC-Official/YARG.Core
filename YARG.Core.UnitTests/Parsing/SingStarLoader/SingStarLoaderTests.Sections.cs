using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing.SingStarLoader;

internal class SingStarLoaderTests_Sections : SingStarLoaderTests
{
    [Test]
    public void ParsePartAttributeCreatesSections()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Chorus\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Chorus\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Bridge\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Bridge\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var sections = loader.LoadSections();

        Assert.That(sections, Has.Count.EqualTo(3));
        Assert.That(sections[0].Name, Is.EqualTo("Verse"));
        Assert.That(sections[1].Name, Is.EqualTo("Chorus"));
        Assert.That(sections[2].Name, Is.EqualTo("Bridge"));
    }

    [Test]
    public void NoPartAttributeReturnsEmptySections()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var sections = loader.LoadSections();

        Assert.That(sections, Has.Count.EqualTo(0));
    }

    [Test]
    public void SamePartAttributeNoDuplicateSections()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"More\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var sections = loader.LoadSections();

        Assert.That(sections, Has.Count.EqualTo(1));
        Assert.That(sections[0].Name, Is.EqualTo("Verse"));
    }

    [Test]
    public void SectionTimingMatchesPartChange()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"8\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Chorus\">",
            "<NOTE MidiNote=\"60\" Duration=\"8\" Lyric=\"Chorus\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var sections = loader.LoadSections();

        Assert.That(sections, Has.Count.EqualTo(2));
        Assert.That(sections[0].Name, Is.EqualTo("Verse"));
        Assert.That(sections[0].Tick, Is.EqualTo(0u));
        Assert.That(sections[1].Name, Is.EqualTo("Chorus"));
        Assert.That(sections[1].Tick, Is.GreaterThan(0u));
    }

    [Test]
    public void DuetPartsCreateSections()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"2\" Tempo=\"120\" Duet=\"Yes\">",
            "<TRACK Name=\"Player1\">",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Player1\"/>",
            "</SENTENCE>",
            "<SENTENCE Part=\"Chorus\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Chorus\"/>",
            "</SENTENCE>",
            "</TRACK>",
            "<TRACK Name=\"Player2\">",
            "<SENTENCE Part=\"Verse\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Player2\"/>",
            "</SENTENCE>",
            "</TRACK>",
            "</MELODY>"
        ));

        var sections = loader.LoadSections();

        // Player1: Verse, Chorus. Player2: Verse (new section since it comes after Player1's Chorus)
        Assert.That(sections, Has.Count.EqualTo(3));
        Assert.That(sections[0].Name, Is.EqualTo("Verse"));
        Assert.That(sections[1].Name, Is.EqualTo("Chorus"));
        Assert.That(sections[2].Name, Is.EqualTo("Verse"));
    }
}
