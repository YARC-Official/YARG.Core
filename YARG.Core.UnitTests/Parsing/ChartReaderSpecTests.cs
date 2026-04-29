using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing;

using static MoonSong;
using static MoonNote;
using static ChartText;

internal class ChartReaderSpecTests
{
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

    [Test]
    public void TrackHeaders_RouteRepresentativeSpecInstrumentsAndDifficulties()
    {
        var song = ChartReader.ReadFromText(Chart(
            SongSection(),
            SyncSection(),
            Section("EasySingle", "0 = N 0 96"),
            Section("MediumDoubleBass", "0 = N 1 96"),
            Section("HardDrums", "0 = N 1 96"),
            Section("ExpertKeyboard", "0 = N 2 96"),
            Section("ExpertGHLGuitar", "0 = N 8 96")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.GetChart(MoonInstrument.Guitar, Difficulty.Easy).notes.Single().guitarFret,
                Is.EqualTo(GuitarFret.Green));
            Assert.That(song.GetChart(MoonInstrument.Bass, Difficulty.Medium).notes.Single().guitarFret,
                Is.EqualTo(GuitarFret.Red));
            Assert.That(song.GetChart(MoonInstrument.Drums, Difficulty.Hard).notes.Single().drumPad,
                Is.EqualTo(DrumPad.Red));
            Assert.That(song.GetChart(MoonInstrument.Keys, Difficulty.Expert).notes.Single().guitarFret,
                Is.EqualTo(GuitarFret.Yellow));
            Assert.That(song.GetChart(MoonInstrument.GHLiveGuitar, Difficulty.Expert).notes.Single().ghliveGuitarFret,
                Is.EqualTo(GHLiveGuitarFret.Black3));
        }
    }

    [Test]
    public void ReservedSpecNoteValues_AreIgnored()
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

}
