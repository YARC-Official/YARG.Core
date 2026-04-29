using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.MoonscraperChartParser.IO.Chart;

using static MoonSong;
using static MoonNote;
using static ChartText;
using static YARG.Core.UnitTests.MoonscraperChartParser.MoonNoteAssertions;

internal class ChartReaderSpecTests
{
    private static readonly TestCaseData[] InstrumentCases =
    [
        new("EasySingle", MoonInstrument.Guitar, Difficulty.Easy, GuitarFret.Red),
        new("MediumDoubleGuitar", MoonInstrument.GuitarCoop, Difficulty.Medium, GuitarFret.Red),
        new("HardDoubleBass", MoonInstrument.Bass, Difficulty.Hard, GuitarFret.Red),
        new("ExpertDoubleRhythm", MoonInstrument.Rhythm, Difficulty.Expert, GuitarFret.Red),
        new("ExpertKeyboard", MoonInstrument.Keys, Difficulty.Expert, GuitarFret.Red),
        new("ExpertDrums", MoonInstrument.Drums, Difficulty.Expert, DrumPad.Red),
        new("ExpertGHLGuitar", MoonInstrument.GHLiveGuitar, Difficulty.Expert, GHLiveGuitarFret.White2),
        new("ExpertGHLBass", MoonInstrument.GHLiveBass, Difficulty.Expert, GHLiveGuitarFret.White2),
        new("ExpertGHLCoop", MoonInstrument.GHLiveCoop, Difficulty.Expert, GHLiveGuitarFret.White2),
        new("ExpertGHLRhythm", MoonInstrument.GHLiveRhythm, Difficulty.Expert, GHLiveGuitarFret.White2),
    ];

    [Test]
    public void ReadFromText_AllowsMissingEventsSectionDespiteSpecListingItAsStandard()
    {
        var song = ChartReader.ReadFromText(Chart(SongSection(), SyncSection()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.events, Is.Empty);
            Assert.That(song.sections, Is.Empty);
        }
    }

    [Test]
    public void ReadFromText_DefaultsMissingResolutionTo192DespiteSpecCallingItMandatory()
    {
        var song = ChartReader.ReadFromText(Chart(
            Section(ChartIOHelper.SECTION_SONG, "Name = \"No Resolution\""),
            SyncSection()));

        Assert.That(song.resolution, Is.EqualTo(Resolution));
    }

    [Test]
    public void SongSection_IgnoresOtherQuotedAndUnquotedMetadata()
    {
        var song = ChartReader.ReadFromText(Chart(
            Section(ChartIOHelper.SECTION_SONG,
                "Name = \"5000 Robots\"",
                "Player2 = bass",
                "Difficulty = 0",
                "Resolution = 480",
                "MusicStream = \"song.ogg\""),
            SyncSection()));

        Assert.That(song.resolution, Is.EqualTo(480));
    }

    [Test]
    public void SyncTrack_ParsesTimeSignatureDefaultDenominatorAsFour()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000", "192 = TS 3")));

        var timeSignature = song.syncTrack.TimeSignatures.Single(ts => ts.Tick == 192);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timeSignature.Numerator, Is.EqualTo(3));
            Assert.That(timeSignature.Denominator, Is.EqualTo(4));
        }
    }

    [Test]
    public void SyncTrack_IgnoresAnchorEvents()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            Section(ChartIOHelper.SECTION_SYNC_TRACK,
                "0 = B 120000",
                "0 = A 123456",
                "192 = B 150000",
                "192 = A 654321")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.syncTrack.Tempos, Has.Count.EqualTo(2));
            Assert.That(song.syncTrack.Tempos.Select(tempo => tempo.Tick), Is.EqualTo(new uint[] { 0, 192 }));
        }
    }

    [Test]
    public void EventsSection_ParsesQuotedTextSectionsAndLyricsAsGlobalText()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section(ChartIOHelper.SECTION_EVENTS,
                "0 = E \"section Verse 1\"",
                "96 = E \"phrase_start\"",
                "120 = E \"lyric OOOoooo\"")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.sections, Has.One.Matches<MoonText>(text =>
                text is { tick: 0, text: "Verse 1" }));
            Assert.That(song.events, Has.One.Matches<MoonText>(text =>
                text is { tick: 96, text: "phrase_start" }));
            Assert.That(song.events, Has.One.Matches<MoonText>(text =>
                text is { tick: 120, text: "lyric OOOoooo" }));
        }
    }

    [Test]
    public void TrackEvents_AcceptUnquotedSpecForm()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle", "168960 = E custom_event")));

        var chart = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert);

        Assert.That(chart.events, Has.One.Matches<MoonText>(text =>
            text is { tick: 168960, text: "custom_event" }));
    }

    [Test]
    public void TrackEvents_AreLenientAndAcceptQuotedForm()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle", "192 = E \"custom_event\"")));

        var chart = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert);

        Assert.That(chart.events, Has.One.Matches<MoonText>(text =>
            text is { tick: 192, text: "custom_event" }));
    }

    [TestCaseSource(nameof(InstrumentCases))]
    public void TrackHeaders_RouteAllSpecInstrumentsAndDifficulties(string sectionName, MoonInstrument instrument,
        Difficulty difficulty, object expectedNote)
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section(sectionName, "0 = N 1 96")));

        var notes = song.GetChart(instrument, difficulty).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            switch (expectedNote)
            {
                case GuitarFret guitarFret:
                    Assert.That(notes[0].guitarFret, Is.EqualTo(guitarFret));
                    break;
                case DrumPad drumPad:
                    Assert.That(notes[0].drumPad, Is.EqualTo(drumPad));
                    break;
                case GHLiveGuitarFret ghlFret:
                    Assert.That(notes[0].ghliveGuitarFret, Is.EqualTo(ghlFret));
                    break;
            }
        }
    }

    [Test]
    public void GuitarNotes_ParseSpecFretFlagsAndOpenNote()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                "0 = N 0 10",
                "20 = N 1 10",
                "40 = N 2 10",
                "60 = N 3 10",
                "80 = N 4 10",
                "100 = N 7 10",
                "100 = N 5 0",
                "120 = N 0 10",
                "120 = N 6 0")));

        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes.Select(note => note.guitarFret), Is.EqualTo(new[]
            {
                GuitarFret.Green,
                GuitarFret.Red,
                GuitarFret.Yellow,
                GuitarFret.Blue,
                GuitarFret.Orange,
                GuitarFret.Open,
                GuitarFret.Green,
            }));
            AssertHasFlag(notes.Single(note => note.tick == 100), Flags.Forced);
            AssertHasFlag(notes.Single(note => note.tick == 120), Flags.Tap);
        }
    }

    [Test]
    public void DrumsNotes_ParseSpecDoubleKickAccentGhostAndCymbalToggles()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertDrums",
                "0 = N 32 0",
                "20 = N 1 0",
                "20 = N 34 0",
                "40 = N 2 0",
                "40 = N 41 0",
                "40 = N 66 0",
                "60 = N 3 0",
                "60 = N 67 0",
                "80 = N 4 0",
                "80 = N 68 0")));

        var notes = song.GetChart(MoonInstrument.Drums, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            AssertHasFlag(notes.Single(note => note.tick == 0), Flags.DoubleKick);
            AssertHasFlag(notes.Single(note => note.tick == 20), Flags.ProDrums_Accent);
            var yellow = notes.Single(note => note.tick == 40);
            AssertHasFlag(yellow, Flags.ProDrums_Ghost);
            AssertHasFlag(yellow, Flags.ProDrums_Cymbal);
            AssertHasFlag(notes.Single(note => note.tick == 60), Flags.ProDrums_Cymbal);
            AssertHasFlag(notes.Single(note => note.tick == 80), Flags.ProDrums_Cymbal);
        }
    }

    [Test]
    public void GhlNotes_ParseSpecFretFlagsAndOpenNote()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertGHLGuitar",
                "0 = N 0 10",
                "20 = N 1 10",
                "40 = N 2 10",
                "60 = N 3 10",
                "80 = N 4 10",
                "100 = N 8 10",
                "120 = N 7 10",
                "120 = N 5 0",
                "140 = N 0 10",
                "140 = N 6 0")));

        var notes = song.GetChart(MoonInstrument.GHLiveGuitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes.Select(note => note.ghliveGuitarFret), Is.EqualTo(new[]
            {
                GHLiveGuitarFret.White1,
                GHLiveGuitarFret.White2,
                GHLiveGuitarFret.White3,
                GHLiveGuitarFret.Black1,
                GHLiveGuitarFret.Black2,
                GHLiveGuitarFret.Black3,
                GHLiveGuitarFret.Open,
                GHLiveGuitarFret.White1,
            }));
            AssertHasFlag(notes.Single(note => note.tick == 120), Flags.Forced);
            AssertHasFlag(notes.Single(note => note.tick == 140), Flags.Tap);
        }
    }

    [Test]
    public void SpecialPhrases_ParseSpecStarpowerCoopAndDrumSpecials()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                "0 = S 2 192",
                "200 = S 0 20",
                "240 = S 1 20"),
            Section("ExpertDrums",
                "0 = S 64 96",
                "120 = S 65 48",
                "200 = S 66 48")));

        var guitarPhrases = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).specialPhrases;
        var drumPhrases = song.GetChart(MoonInstrument.Drums, Difficulty.Expert).specialPhrases;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(guitarPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 0, length: 192, type: MoonPhrase.Type.Starpower }));
            Assert.That(guitarPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 200, length: 20, type: MoonPhrase.Type.Versus_Player1 }));
            Assert.That(guitarPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 240, length: 20, type: MoonPhrase.Type.Versus_Player2 }));
            Assert.That(drumPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 0, length: 96, type: MoonPhrase.Type.ProDrums_Activation }));
            Assert.That(drumPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 120, length: 48, type: MoonPhrase.Type.TremoloLane }));
            Assert.That(drumPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 200, length: 48, type: MoonPhrase.Type.TrillLane }));
        }
    }

    [Test]
    public void UnknownReservedNoteValues_AreIgnored()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                "0 = N 96 10",
                "20 = N 127 10")));

        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        Assert.That(notes, Is.Empty);
    }

}
