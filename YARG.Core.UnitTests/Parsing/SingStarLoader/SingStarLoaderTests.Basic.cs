using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing.SingStarLoader;

internal class SingStarLoaderTests_Basic : SingStarLoaderTests
{
    [Test]
    public void ParseMinimalFile()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<!-- Artist: Test Artist -->",
            "<!-- Title: Test Song -->",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("ARTIST"), Is.EqualTo("Test Artist"));
        Assert.That(loader.GetMetadata("TITLE"), Is.EqualTo("Test Song"));
    }

    [Test]
    public void ParseTempo()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"140\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(140f));
    }

    [Test]
    public void ParseTempoWithComma()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"107,37\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(107.37).Within(0.01));
    }

    [Test]
    public void ParseGenre()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\" Genre=\"Pop\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("GENRE"), Is.EqualTo("Pop"));
    }

    [Test]
    public void ParseYear()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\" Year=\"2026\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("YEAR"), Is.EqualTo("2026"));
    }

    [Test]
    public void ParseDuet()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\" Duet=\"Yes\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("PARTS"), Is.EqualTo("2"));
    }

    [Test]
    public void ParseVersionAttribute()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(120f));
    }

    [Test]
    public void ParseResolution()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\" Resolution=\"Demisemiquaver\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("RESOLUTION"), Is.EqualTo("Demisemiquaver"));
    }

    [Test]
    public void ParseNotes()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));

        var phrase = track.Parts[0].NotePhrases[0];
        Assert.That(phrase.PhraseParentNote.ChildNotes, Has.Count.EqualTo(2));
        Assert.That(phrase.Lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(phrase.Lyrics[1].Text, Is.EqualTo("World"));
    }

    [Test]
    public void ParseNotesWithPitches()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"55\" Duration=\"4\" Lyric=\"Low\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Mid\"/>",
            "<NOTE MidiNote=\"67\" Duration=\"4\" Lyric=\"High\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var phrase = track.Parts[0].NotePhrases[0];

        // SingStar pitch is absolute MIDI - no conversion needed
        Assert.That(phrase.PhraseParentNote.ChildNotes[0].Pitch, Is.EqualTo(55f));
        Assert.That(phrase.PhraseParentNote.ChildNotes[1].Pitch, Is.EqualTo(60f));
        Assert.That(phrase.PhraseParentNote.ChildNotes[2].Pitch, Is.EqualTo(67f));
    }

    [Test]
    public void ParseNoteDurations()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Short\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"8\" Lyric=\"Long\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        var shortNote = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var longNote = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[1];

        // Duration is in 1/8-note units, converted to ticks
        // 4 vs 8 units at 8 units/beat = 0.5 vs 1 beat at 120 BPM
        Assert.That(longNote.TickLength, Is.EqualTo(shortNote.TickLength * 2));
    }

    [Test]
    public void ParseRestNotes()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Hello\"/>",
            "<NOTE MidiNote=\"0\" Duration=\"16\" Lyric=\"\"/>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"World\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Long rest (16 units >= threshold) between notes creates separate phrases
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(track.Parts[0].NotePhrases[1].Lyrics[0].Text, Is.EqualTo("World"));
    }

    [Test]
    public void ParseMultipleMetadata()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"130\" Genre=\"Rock\" Year=\"2024\" Duet=\"Yes\">",
            "<!-- Artist: My Artist -->",
            "<!-- Title: My Song -->",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        Assert.That(loader.GetMetadata("TITLE"), Is.EqualTo("My Song"));
        Assert.That(loader.GetMetadata("ARTIST"), Is.EqualTo("My Artist"));
        Assert.That(loader.GetMetadata("GENRE"), Is.EqualTo("Rock"));
        Assert.That(loader.GetMetadata("YEAR"), Is.EqualTo("2024"));
        Assert.That(loader.GetMetadata("PARTS"), Is.EqualTo("2"));
    }

    [Test]
    public void IgnoreInvalidElements()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Valid\"/>",
            "<INVALID/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Valid"));
    }

    [Test]
    public void EmptySentenceHandled()
    {
        var loader = LoadSingStar(Ss(
            "<MELODY Version=\"1\" Tempo=\"120\">",
            "<SENTENCE>",
            "<NOTE MidiNote=\"0\" Duration=\"4\" Lyric=\"\"/>",
            "</SENTENCE>",
            "<SENTENCE>",
            "<NOTE MidiNote=\"60\" Duration=\"4\" Lyric=\"Test\"/>",
            "</SENTENCE>",
            "</MELODY>"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Test"));
    }
}