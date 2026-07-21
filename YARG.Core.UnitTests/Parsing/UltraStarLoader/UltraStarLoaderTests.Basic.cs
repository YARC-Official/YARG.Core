using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing;

internal class UltraStarLoaderTests_Basic : UltraStarLoaderTests
{
    [Test]
    public void ParseMinimalFile()
    {
        var loader = LoadUltraStar(Us(
            "#TITLE:Test Song",
            "#ARTIST:Test Artist",
            "#BPM:120",
            ": 0 4 0 Hello"
        ));

        Assert.That(loader.GetMetadata("TITLE"), Is.EqualTo("Test Song"));
        Assert.That(loader.GetMetadata("ARTIST"), Is.EqualTo("Test Artist"));
    }

    [Test]
    public void ParseBpm()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:140",
            ": 0 4 0 Test"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(140f));
    }

    [Test]
    public void ParseBpmWithComma()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120,5",
            ": 0 4 0 Test"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(120.5f));
    }

    [Test]
    public void ParseGap()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            "#GAP:1000",
            ": 0 4 0 Test"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].Time, Is.EqualTo(-1.0).Within(0.001));
    }

    [Test]
    public void ParseGapWithComma()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            "#GAP:1500,5",
            ": 0 4 0 Test"
        ));

        var syncTrack = loader.LoadSyncTrack();
        Assert.That(syncTrack.Tempos[0].Time, Is.EqualTo(-1.5005).Within(0.001));
    }

    [Test]
    public void ParseNotes()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 Hello",
            "- 5",
            ": 5 4 2 world",
            "- 10",
            ": 10 2 -1 test"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(3));

        var phrase1 = track.Parts[0].NotePhrases[0];
        Assert.That(phrase1.PhraseParentNote.ChildNotes, Has.Count.EqualTo(1));
        Assert.That(phrase1.Lyrics[0].Text, Is.EqualTo("Hello"));
    }

    [Test]
    public void ParseNotesWithPitches()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 Hello",
            "- 5",
            ": 5 4 4 world",
            "- 10",
            ": 10 4 -3 test"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Pitch = UltraStar pitch + 60 (MIDI base)
        var phrase1 = track.Parts[0].NotePhrases[0];
        Assert.That(phrase1.PhraseParentNote.ChildNotes[0].Pitch, Is.EqualTo(60f)); // 0 + 60

        var phrase2 = track.Parts[0].NotePhrases[1];
        Assert.That(phrase2.PhraseParentNote.ChildNotes[0].Pitch, Is.EqualTo(64f)); // 4 + 60

        var phrase3 = track.Parts[0].NotePhrases[2];
        Assert.That(phrase3.PhraseParentNote.ChildNotes[0].Pitch, Is.EqualTo(57f)); // -3 + 60
    }

    [Test]
    public void ParseNoteDurations()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 short",
            "- 5",
            ": 5 8 0 long"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        var shortNote = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];
        var longNote = track.Parts[0].NotePhrases[1].PhraseParentNote.ChildNotes[0];

        // Duration is in beats, converted to ticks
        // 4 beats vs 8 beats at 120 BPM = 480 vs 960 ticks (with 120 ticks/beat)
        Assert.That(longNote.TickLength, Is.EqualTo(shortNote.TickLength * 2));
    }

    [Test]
    public void ParseRestSeparator()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 Hello",
            "- 5",
            ": 10 4 0 World"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);

        // Should create 2 phrases separated by rest
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(2));
        Assert.That(track.Parts[0].NotePhrases[0].Lyrics[0].Text, Is.EqualTo("Hello"));
        Assert.That(track.Parts[0].NotePhrases[1].Lyrics[0].Text, Is.EqualTo("World"));
    }

    [Test]
    public void ParseFreestyleNote()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            "F 0 4 -1 Scream"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var note = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // Freestyle should be unpitched (-1)
        Assert.That(note.IsNonPitched, Is.True);
    }

    [Test]
    public void ParseMultipleMetadata()
    {
        var loader = LoadUltraStar(Us(
            "#TITLE:My Song",
            "#ARTIST:My Artist",
            "#ALBUM:My Album",
            "#YEAR:2024",
            "#GENRE:Rock",
            "#CREATOR:Me",
            "#BPM:130",
            "#GAP:500",
            ": 0 4 0 Test"
        ));

        Assert.That(loader.GetMetadata("TITLE"), Is.EqualTo("My Song"));
        Assert.That(loader.GetMetadata("ARTIST"), Is.EqualTo("My Artist"));
        Assert.That(loader.GetMetadata("ALBUM"), Is.EqualTo("My Album"));
        Assert.That(loader.GetMetadata("YEAR"), Is.EqualTo("2024"));
        Assert.That(loader.GetMetadata("GENRE"), Is.EqualTo("Rock"));
        Assert.That(loader.GetMetadata("CREATOR"), Is.EqualTo("Me"));
    }

    [Test]
    public void IgnoreInvalidLines()
    {
        var loader = LoadUltraStar(Us(
            "#VALID:value",
            "invalid line here",
            ": 0 4 0 Test",
            "#ANOTHER:valid"
        ));

        Assert.That(loader.GetMetadata("VALID"), Is.EqualTo("value"));
        Assert.That(loader.GetMetadata("ANOTHER"), Is.EqualTo("valid"));
    }

    [Test]
    public void EmptyLinesIgnored()
    {
        var loader = LoadUltraStar(Us(
            "",
            "#BPM:120",
            "",
            ": 0 4 0 Test",
            ""
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        Assert.That(track.Parts[0].NotePhrases, Has.Count.EqualTo(1));
    }
}