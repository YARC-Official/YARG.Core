using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.MoonscraperChartParser.IO.Chart;

using static MoonSong;
using static MoonNote;
using static ChartText;
using static YARG.Core.UnitTests.MoonscraperChartParser.MoonNoteAssertions;

public class ChartReaderProcessListsTests
{
    [Test]
    public void ReadFromText_ThrowsWhenSongSectionIsMissing()
    {
        var chartText = Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000");

        Assert.Throws<InvalidDataException>(() => ChartReader.ReadFromText(chartText));
    }

    [Test]
    public void ReadFromText_ThrowsWhenRequiredSectionsAreOutOfOrder()
    {
        var chartText = Chart(
            Section(ChartIOHelper.SECTION_SYNC_TRACK, "0 = B 120000"),
            Section(ChartIOHelper.SECTION_SONG, "Resolution = 192"));

        Assert.Throws<InvalidDataException>(() => ChartReader.ReadFromText(chartText));
    }

    [Test]
    public void ReadFromText_ParsesSongAndSyncSections()
    {
        var chartText = Chart(
            SongSection(resolution: 480),
            Section(ChartIOHelper.SECTION_SYNC_TRACK,
                "0 = B 150000",
                "240 = TS 7 3"));

        var song = ChartReader.ReadFromText(chartText);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.resolution, Is.EqualTo(480));
            Assert.That(song.hopoThreshold, Is.EqualTo(162));
            Assert.That(song.syncTrack.Tempos, Has.Count.EqualTo(1));
            Assert.That(song.syncTrack.Tempos[0].BeatsPerMinute, Is.EqualTo(150));
            Assert.That(song.syncTrack.TimeSignatures, Has.Count.EqualTo(2));
            Assert.That(song.syncTrack.TimeSignatures[^1].Tick, Is.EqualTo(240));
            Assert.That(song.syncTrack.TimeSignatures[^1].Numerator, Is.EqualTo(7));
            Assert.That(song.syncTrack.TimeSignatures[^1].Denominator, Is.EqualTo(8));
        }
    }

    [Test]
    public void ReadFromText_ParsesGlobalSectionsAndEvents()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section(ChartIOHelper.SECTION_EVENTS,
                "0 = E \"section Intro\"",
                "192 = E \"music_start\""));

        var song = ChartReader.ReadFromText(chartText);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(song.sections, Has.One.Matches<MoonText>(text =>
                text.tick == 0 && text.text == "Intro"));
            Assert.That(song.events, Has.One.Matches<MoonText>(text =>
                text.tick == 192 && text.text == "music_start"));
        }
    }

    [Test]
    public void GuitarProcessList_AppliesTapOverForcedAfterNotesAreParsed()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                "0 = N 0 192",
                "0 = N 5 0",
                "0 = N 6 0"));

        var song = ChartReader.ReadFromText(chartText);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].guitarFret, Is.EqualTo(GuitarFret.Green));
            AssertHasFlag(notes[0], Flags.Tap);
            AssertDoesNotHaveFlag(notes[0], Flags.Forced);
        }
    }

    [Test]
    public void GuitarProcessList_ConvertsSoloTextEventsToInclusiveSoloPhrase()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                $"100 = E \"{TextEvents.SOLO_START}\"",
                $"199 = E \"{TextEvents.SOLO_END}\""));

        var song = ChartReader.ReadFromText(chartText);
        var chart = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chart.events, Is.Empty);
            Assert.That(chart.specialPhrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 100, length: 100, type: MoonPhrase.Type.Solo }));
        }
    }

    [Test]
    public void GuitarProcessList_KeepsBackToBackSolosFromOverlapping()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle",
                $"100 = E \"{TextEvents.SOLO_START}\"",
                $"200 = E \"{TextEvents.SOLO_START}\"",
                $"200 = E \"{TextEvents.SOLO_END}\"",
                $"300 = E \"{TextEvents.SOLO_END}\""));

        var song = ChartReader.ReadFromText(chartText);
        var phrases = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).specialPhrases;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(phrases, Has.Count.EqualTo(2));
            Assert.That(phrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 100, length: 100, type: MoonPhrase.Type.Solo }));
            Assert.That(phrases, Has.One.Matches<MoonPhrase>(phrase =>
                phrase is { tick: 200, length: 101, type: MoonPhrase.Type.Solo }));
        }
    }

    [Test]
    public void DrumsProcessList_AppliesCymbalAndDynamicsAfterNotesAreParsed()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertDrums",
                "0 = N 2 192",
                $"0 = N {ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2} 0",
                $"0 = N {ChartIOHelper.NOTE_OFFSET_DRUMS_ACCENT + 2} 0",
                $"0 = N {ChartIOHelper.NOTE_OFFSET_DRUMS_GHOST + 2} 0"));

        var song = ChartReader.ReadFromText(chartText);
        var notes = song.GetChart(MoonInstrument.Drums, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].drumPad, Is.EqualTo(DrumPad.Yellow));
            AssertHasFlag(notes[0], Flags.ProDrums_Cymbal);
            AssertHasFlag(notes[0], Flags.ProDrums_Accent);
            AssertDoesNotHaveFlag(notes[0], Flags.ProDrums_Ghost);
        }
    }

    [Test]
    public void DrumsProcessList_DisambiguatesUnknownDrumsTypeFromCymbalMarkers()
    {
        var settings = ParseSettings.Default_Chart;
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertDrums",
                "0 = N 2 192",
                $"0 = N {ChartIOHelper.NOTE_OFFSET_PRO_DRUMS + 2} 0"));

        ChartReader.ReadFromText(ref settings, chartText);

        Assert.That(settings.DrumsType, Is.EqualTo(DrumsType.FourLane));
    }

    [Test]
    public void DrumsProcessList_DisambiguatesUnknownDrumsTypeFromFiveLaneGreen()
    {
        var settings = ParseSettings.Default_Chart;
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertDrums", "0 = N 5 192"));

        ChartReader.ReadFromText(ref settings, chartText);

        Assert.That(settings.DrumsType, Is.EqualTo(DrumsType.FiveLane));
    }

    [Test]
    public void ReadFromText_AppliesSustainCutoffThreshold()
    {
        var settings = ParseSettings.Default_Chart;
        settings.SustainCutoffThreshold = 10;
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertSingle", "0 = N 0 9", "100 = N 1 10"));

        var song = ChartReader.ReadFromText(ref settings, chartText);
        var notes = song.GetChart(MoonInstrument.Guitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(2));
            Assert.That(notes.Single(note => note.tick == 0).length, Is.EqualTo(0));
            Assert.That(notes.Single(note => note.tick == 100).length, Is.EqualTo(10));
        }
    }

    [Test]
    public void GhlProcessList_ParsesGhlSpecificNoteMapAndTapFlag()
    {
        var chartText = Chart(
            SongSection(),
            SyncSection(),
            Section("ExpertGHLGuitar",
                "0 = N 8 192",
                "0 = N 6 0"));

        var song = ChartReader.ReadFromText(chartText);
        var notes = song.GetChart(MoonInstrument.GHLiveGuitar, Difficulty.Expert).notes;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].ghliveGuitarFret, Is.EqualTo(GHLiveGuitarFret.Black3));
            AssertHasFlag(notes[0], Flags.Tap);
        }
    }

}
