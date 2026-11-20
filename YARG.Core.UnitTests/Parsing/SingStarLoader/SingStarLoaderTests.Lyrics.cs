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
    public void ParseWordJoinWithSpacedDash()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Be -\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"cause\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // "Be -" is a word-internal syllable boundary, not a melisma: the first syllable
        // keeps a join marker and the second stays a plain syllable.
        Assert.That(lyrics[0].Text, Is.EqualTo("Be-"));
        Assert.That(lyrics[0].JoinWithNext, Is.True);
        Assert.That(lyrics[1].Text, Is.EqualTo("cause"));
        Assert.That(lyrics[1].JoinWithNext, Is.False);
    }

    [Test]
    public void ParseAttachedHyphenJoinsWord()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"wo-\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"rld\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // The source hyphen is preserved without doubling it, and the words are joined.
        Assert.That(lyrics[0].Text, Is.EqualTo("wo-"));
        Assert.That(lyrics[0].JoinWithNext, Is.True);
        Assert.That(lyrics[1].Text, Is.EqualTo("rld"));
    }

    [Test]
    public void ParseDanglingWordJoinIsDropped()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Be -\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // There is no following syllable to join, so the marker must not leak.
        Assert.That(lyrics, Has.Count.EqualTo(1));
        Assert.That(lyrics[0].Text, Is.EqualTo("Be"));
        Assert.That(lyrics[0].JoinWithNext, Is.False);
    }

    [Test]
    public void ParseStandaloneDash()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hold\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"-\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // A lone "-" is a held note: it slides onto the note before it and has no syllable.
        Assert.That(lyrics, Has.Count.EqualTo(3));
        Assert.That(lyrics[0].Text, Is.EqualTo("Hold"));
        Assert.That(lyrics[1].Text, Is.EqualTo("+"));
        Assert.That(lyrics[2].Text, Is.EqualTo("test"));
    }

    [Test]
    public void ParseStandaloneDashWithoutPreviousNote()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"-\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var phrase = track.Parts[0].NotePhrases[0];

        // Nothing precedes the hold, so there is no slide or syllable for it.
        Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(2));
        Assert.That(phrase.Lyrics, Has.Count.EqualTo(1));
        Assert.That(phrase.Lyrics[0].Text, Is.EqualTo("test"));
    }

    [Test]
    public void ParseFreestyleAsUnpitched()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Scream\" FreeStyle=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var phrase = track.Parts[0].NotePhrases[0];
        var note = phrase.PhraseParentNote.ChildNotes[0];

        Assert.That(note.Pitch, Is.EqualTo(-1f));
        Assert.That(phrase.Lyrics[0].Text, Is.EqualTo("Scream#"));
        Assert.That(phrase.Lyrics[0].NonPitched, Is.True);
    }

    [Test]
    public void ParseLiteralPlusIsEscaped()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"C+\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var lyrics = track.Parts[0].NotePhrases[0].Lyrics;

        // '+' is YARG's pitch-slide marker, so a literal one must not merge notes.
        Assert.That(lyrics[0].Text, Is.EqualTo("C＋"));
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
    public void ParseLyricsTrackStripsMarkers()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Be -\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"-\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"cause\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var events = loader.LoadLyrics().Phrases[0].Lyrics;

        // "Be-" is displayed as "Be" with a join flag, the held note carries the slide
        // flag, and the final syllable is plain.
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0].Text, Is.EqualTo("Be"));
        Assert.That(events[0].JoinWithNext, Is.True);
        Assert.That(events[1].Text, Is.Empty);
        Assert.That(events[1].PitchSlide, Is.True);
        Assert.That(events[2].Text, Is.EqualTo("cause"));
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

    [Test]
    public void MelismaMergesIntoPreviousNoteInFinalChart()
    {
        var chart = LoadSingStarChart(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hold\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"-\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var notes = chart.Vocals.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;

        // The held note is a pitch slide, so it becomes a child of the note before it.
        Assert.That(notes, Has.Count.EqualTo(2));
        Assert.That(notes[0].ChildNotes, Has.Count.EqualTo(1));
        Assert.That(notes[0].ChildNotes[0].Pitch, Is.EqualTo(62f));
        Assert.That(notes[1].Pitch, Is.EqualTo(60f));
    }

    [Test]
    public void FreestyleNoteIsUnpitchedInFinalChart()
    {
        var chart = LoadSingStarChart(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Scream\" FreeStyle=\"Yes\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var notes = chart.Vocals.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;

        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0].IsNonPitched, Is.True);
        Assert.That(notes[0].Pitch, Is.EqualTo(-1f));
    }

    [Test]
    public void WordJoinSurvivesTheFinalChart()
    {
        var chart = LoadSingStarChart(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Be -\"/>",
            "<NOTE MidiNote=\"62\" Duration=\"4\" Lyric=\"cause\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var lyrics = chart.Vocals.Parts[0].NotePhrases[0].Lyrics;

        Assert.That(lyrics, Has.Count.EqualTo(2));
        Assert.That(lyrics[0].Text, Is.EqualTo("Be-"));
        Assert.That(lyrics[0].JoinWithNext, Is.True);
        Assert.That(lyrics[1].Text, Is.EqualTo("cause"));
        Assert.That(lyrics[1].JoinWithNext, Is.False);
    }
}