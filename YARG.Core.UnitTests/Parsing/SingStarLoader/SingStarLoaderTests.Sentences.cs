using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing.SingStarLoader;

internal class SingStarLoaderTests_Sentences : SingStarLoaderTests
{
    [Test]
    public void ParseSentenceBoundaries()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Each SENTENCE should be a separate phrase
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(3));
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(track.Parts[0].NotePhrases[1].Lyrics[0].Text, Is.EqualTo("World"));
        Assert.That(track.Parts[0].NotePhrases[2].Lyrics[0].Text, Is.EqualTo("Test"));
    }

    [Test]
    public void ParseSentenceWithLeadingRest()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"0\" Duration=\"4\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Leading rest should not create extra phrase
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Hello"));
    }

    [Test]
    public void ParseSentenceAttributes()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE Singer=\"Solo 1\" Part=\"Chorus\">",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Should parse notes even without explicit Singer/Part attributes
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShortRestBetweenSentencesDoesNotMerge()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"0\" Duration=\"3\" Lyric=\"\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Short rest (3 units < 16 units threshold) should NOT merge sentences
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
    }

    [Test]
    public void LongRestBetweenSentencesCreatesSeparatePhrase()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"0\" Duration=\"20\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Long rest (20 units >= 16 units threshold) should create separate phrase
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
    }

    [Test]
    public void MultipleSentencesInSequence()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"One\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Two\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Three\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Four\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Five\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(5));
    }

    [Test]
    public void EmptySentencesIgnored()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"0\" Duration=\"4\" Lyric=\"\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"0\" Duration=\"4\" Lyric=\"\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Empty sentences should be filtered out
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
    }
}