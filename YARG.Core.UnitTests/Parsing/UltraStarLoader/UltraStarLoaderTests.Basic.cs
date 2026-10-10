using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;

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
        // UltraStar BPM is halved for SyncTrack (beatlines/crowd clapping)
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(70f));
    }

    [Test]
    public void ParseBpmWithComma()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120,5",
            ": 0 4 0 Test"
        ));

        var syncTrack = loader.LoadSyncTrack();
        // UltraStar BPM is halved for SyncTrack
        Assert.That(syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(60.25f));
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
        // GAP lives only in ticks, not in the sync track's start time.
        Assert.That(syncTrack.Tempos[0].Time, Is.EqualTo(0.0).Within(0.001));
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
        Assert.That(syncTrack.Tempos[0].Time, Is.EqualTo(0.0).Within(0.001));
    }

    [Test]
    public void GapShiftsFirstNoteByExactlyOneGapNotTwo()
    {
        // In the final chart GAP takes effect only through BeatToTick (MoonSong.AddTempo
        // drops LoadSyncTrack's negative start time), so any second GAP shift, such as
        // SongOffset, would double it.
        var songChart = LoadUltraStarChart(Us(
            "#BPM:120",
            "#GAP:2500",
            ": 0 4 0 Hello"
        ));

        double firstNoteTime = songChart.Vocals.Parts[0].NotePhrases[0].PhraseParentNote.Time;
        Assert.That(firstNoteTime, Is.EqualTo(2.5).Within(0.001));
    }

    [Test]
    public void ConsecutiveBareTildeFreestyleNotesStayUnpitchedInFinalChart()
    {
        // MoonSongLoader.Vocals decides whether a note is pitched from its lyric's '#', not
        // from VocalNote.Pitch, so a syllable-less unpitched note still needs a lyric event.
        var songChart = LoadUltraStarChart(Us(
            "#BPM:120",
            ": 419 12 21  sta",
            "F 432 3 23 ~",
            "F 436 5 21 ~",
            "F 442 8 16 ~"
        ));

        var notes = songChart.Vocals.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes;
        Assert.That(notes, Has.Count.EqualTo(4));
        Assert.That(notes[0].IsNonPitched, Is.False);
        Assert.That(notes[1].IsNonPitched, Is.True);
        Assert.That(notes[2].IsNonPitched, Is.True);
        Assert.That(notes[3].IsNonPitched, Is.True);
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

    // Freestyle (F), Rap (R) and Golden Rap (G) carry no pitch requirement per spec, but
    // they are unpitched *lyrics* -- not Percussion, which is a separate hit-based mechanic.
    [TestCase("F 0 4 3 Scream", TestName = "Freestyle notes are unpitched lyrics")]
    [TestCase("R 0 4 5 RapBar", TestName = "Rap notes are unpitched lyrics")]
    [TestCase("G 0 4 7 GoldenScream", TestName = "Golden rap notes are unpitched lyrics")]
    public void UnpitchedNoteTypesAreLyricNotPercussion(string noteLine)
    {
        var loader = LoadUltraStar(Us("#BPM:120", noteLine));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var note = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(note.Type, Is.EqualTo(VocalNoteType.Lyric));
            Assert.That(note.IsPercussion, Is.False);
            Assert.That(note.IsNonPitched, Is.True);
            Assert.That(note.Pitch, Is.EqualTo(-1f));
        }
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

    [Test]
    public void MidSongTempoChangeAffectsNoteTiming()
    {
        // At 120 BPM, an UltraStar beat is 15 ticks at 120 ticks/quarter-note, i.e. 0.125s,
        // so beat 20 lands at 2.5s. After "B 20 240" (double tempo), each beat afterward
        // takes half as long.
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 Before",
            "- 5",
            "B 20 240",
            ": 20 4 0 AtChange",
            "- 25",
            ": 30 4 0 After"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var atChangeNote = track.Parts[0].NotePhrases[1].PhraseParentNote.ChildNotes[0];
        var afterNote = track.Parts[0].NotePhrases[2].PhraseParentNote.ChildNotes[0];

        // Beat 20 at 120 BPM = 2.5s (tempo hasn't changed yet at this exact beat).
        Assert.That(atChangeNote.Time, Is.EqualTo(2.5).Within(0.001));
        // Beats 20-30 occur entirely after the change, at 240 BPM (0.0625s/beat): 2.5 + 10 * 0.0625 = 3.125s.
        Assert.That(afterNote.Time, Is.EqualTo(3.125).Within(0.001));
    }

    [Test]
    public void MidSongTempoChangeAffectsSyncTrackTempos()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            ": 0 4 0 Before",
            "- 5",
            "B 20 240",
            ": 20 4 0 After"
        ));

        var syncTrack = loader.LoadSyncTrack();

        Assert.That(syncTrack.Tempos, Has.Count.EqualTo(2));
        // Halved for the SyncTrack, same as the initial tempo.
        Assert.That(syncTrack.Tempos[1].BeatsPerMinute, Is.EqualTo(120f));
        Assert.That(syncTrack.Tempos[1].Time, Is.EqualTo(2.5).Within(0.001));
    }

    [Test]
    public void MidSongTempoChangeStaysAlignedWithNotesWhenGapIsNonZero()
    {
        // GAP is a pure time shift, so it must move every note by the same amount, including
        // ones after a tempo change. That holds only if LoadSyncTrack puts tempo changes in
        // the same tick space as BeatToTick, GAP included.
        string Chart(string gapTag) => Us(
            "#BPM:120",
            gapTag,
            ": 0 4 0 Before",
            "- 5",
            "B 20 240",
            ": 20 4 0 AtChange",
            "- 25",
            ": 30 4 0 After"
        );

        double NoteTime(string chart, int phraseIndex) =>
            LoadUltraStarChart(chart).Vocals.Parts[0].NotePhrases[phraseIndex].PhraseParentNote.Time;

        double atChangeNoGap = NoteTime(Chart(""), 1);
        double afterNoGap = NoteTime(Chart(""), 2);

        double atChangeWithGap = NoteTime(Chart("#GAP:2500"), 1);
        double afterWithGap = NoteTime(Chart("#GAP:2500"), 2);

        Assert.That(atChangeWithGap - atChangeNoGap, Is.EqualTo(2.5).Within(0.001));
        Assert.That(afterWithGap - afterNoGap, Is.EqualTo(2.5).Within(0.001));
    }

    [Test]
    public void GapIsExposedAsRawMetadata()
    {
        // Timing is covered by GapShiftsFirstNoteByExactlyOneGapNotTwo; this checks the raw tag.
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            "#GAP:2500",
            ": 0 4 0 Test"
        ));

        Assert.That(loader.GetMetadata("GAP"), Is.EqualTo("2500"));
    }

    [Test]
    public void NegativeGapClampsToZeroTicks()
    {
        var loader = LoadUltraStar(Us(
            "#BPM:120",
            "#GAP:-1000",
            ": 20 4 0 Test"
        ));

        var track = loader.LoadVocalsTrack(Instrument.Vocals);
        var firstNote = track.Parts[0].NotePhrases[0].PhraseParentNote.ChildNotes[0];

        // A negative GAP can't push a note's tick below 0, so it's clamped out entirely.
        Assert.That(firstNote.Tick, Is.EqualTo(20u * 15));
    }

    [Test]
    public void LoaderTimesMatchSongChartTimes()
    {
        // One timing model: the loader's own Time values and the ticks SongChart ends up
        // with (via MoonSong's tempo map) must never drift apart.
        string chart = Us(
            "#BPM:120",
            "#GAP:1000",
            ": 0 4 0 Before",
            "- 5",
            "B 20 240",
            ": 20 4 0 AtChange",
            "- 25",
            ": 30 4 0 After"
        );

        var loader = LoadUltraStar(chart);
        var loaderTrack = loader.LoadVocalsTrack(Instrument.Vocals);
        var loaderNotes = loaderTrack.Parts[0].NotePhrases
            .Select(p => p.PhraseParentNote.ChildNotes[0].Time)
            .ToList();

        var songChart = LoadUltraStarChart(chart);
        var chartNotes = songChart.Vocals.Parts[0].NotePhrases
            .Select(p => p.PhraseParentNote.ChildNotes[0].Time)
            .ToList();

        Assert.That(chartNotes, Is.EqualTo(loaderNotes).Within(0.001));
    }
}