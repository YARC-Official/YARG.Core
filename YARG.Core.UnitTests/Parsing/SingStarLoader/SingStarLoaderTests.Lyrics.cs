using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing.SingStarLoader;

internal class SingStarLoaderTests_Lyrics : SingStarLoaderTests
{
    [Test]
    public void ParseBasicLyrics()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        Assert.That(lyrics, Has.Count.EqualTo(3));
        Assert.That(lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(lyrics[1].Text, Is.EqualTo("World"));
        Assert.That(lyrics[2].Text, Is.EqualTo("Test"));
    }

    [Test]
    public void ParseMelismaWithDash()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Be -\"/>",
            "<NOTE MidiNote=\"0\" Duration=\"2\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"cause\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // "Be -" starts melisma: first note = "Be", second note = "cause+" (with + marker)
        Assert.That(lyrics[0].Text, Is.EqualTo("Be"));
        Assert.That(lyrics[1].Text, Is.EqualTo("cause+"));
    }

    [Test]
    public void ParseHyphenatedWords()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"cer -\"/>",
            "<NOTE MidiNote=\"0\" Duration=\"1\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"1\" Lyric=\"tain\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // "cer -" starts melisma: first note = "cer", second note = "tain+"
        Assert.That(lyrics[0].Text, Is.EqualTo("cer"));
        Assert.That(lyrics[1].Text, Is.EqualTo("tain+"));
    }

    [Test]
    public void ParseStandaloneDash()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"-\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // Standalone "-" should be converted to "+"
        Assert.That(lyrics[0].Text, Is.EqualTo("+"));
        Assert.That(lyrics[1].Text, Is.EqualTo("test"));
    }

    [Test]
    public void ParseEmptyLyrics()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"0\" Duration=\"4\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // Empty lyrics should be filtered out
        Assert.That(lyrics, Has.Count.EqualTo(1));
        Assert.That(lyrics[0].Text, Is.EqualTo("Test"));
    }

    [Test]
    public void WhitespaceTrimmed()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"  Hello   \"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"  World  \"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        Assert.That(lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(lyrics[1].Text, Is.EqualTo("World"));
    }

    [Test]
    public void ParseLyricsTrack()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var lyricsTrack = loader.LoadLyrics();

        Assert.That(lyricsTrack.Phrases, Has.Count.EqualTo(1));
        Assert.That(lyricsTrack.Phrases[0].Lyrics, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParsePhraseWithMultipleLyrics()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"Hel\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"lo\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"Wor\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"2\" Lyric=\"ld\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var phrase = track.Parts[0].NotePhrases[0];

        Assert.That(phrase.Lyrics, Has.Count.EqualTo(4));
        Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(4));
    }
}