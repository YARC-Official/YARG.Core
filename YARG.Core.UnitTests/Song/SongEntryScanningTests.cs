using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song;
using ChartFormat = YARG.Core.Song.ChartFormat;
using MoonDifficulty = MoonscraperChartEditor.Song.MoonSong.Difficulty;

namespace YARG.Core.UnitTests.Song;

public class SongEntryScanningTests
{
    private const short Resolution = 192;

    [Test]
    public void FinalizeDrums_KeepsFourLaneDifficultiesWhenFourLaneFlagIsPresent()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Hard);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.FourLane);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Hard));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
        }
    }

    [Test]
    public void FinalizeDrums_CopiesFourLaneDifficultiesToProDrumsWhenChartIsProOnly()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Expert);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.ProDrums);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
        }
    }

    [Test]
    public void FinalizeDrums_MovesFourLaneDifficultiesToFiveLaneWhenChartIsFiveLaneOnly()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Medium);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.FiveLane);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Medium));
        }
    }

    [Test]
    public void IsValid_ReturnsFalseForDefaultParts()
    {
        Assert.That(TestSongEntry.IsValidForTest(AvailableParts.Default), Is.False);
    }

    [Test]
    public void IsValid_ReturnsTrueForRepresentativeActiveParts()
    {
        var guitarParts = AvailableParts.Default;
        guitarParts.FiveFretGuitar.ActivateDifficulty(Difficulty.Easy);

        var drumParts = AvailableParts.Default;
        drumParts.ProDrums.ActivateDifficulty(Difficulty.Hard);

        var proKeysParts = AvailableParts.Default;
        proKeysParts.ProKeys.ActivateDifficulty(Difficulty.Expert);

        var vocalParts = AvailableParts.Default;
        vocalParts.HarmonyVocals.ActivateSubtrack(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TestSongEntry.IsValidForTest(guitarParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(drumParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(proKeysParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(vocalParts), Is.True);
        }
    }

    [Test]
    public void ParseMidi_UsesFirstRecognizedTrackNameWhenMultipleTickZeroNamesArePresent()
    {
        var midi = new MidiFile(MakeSyncTrack(), MakeGuitarTrackWithDuplicateNames())
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
        };

        using var stream = new MemoryStream();
        midi.Write(stream);
        stream.Position = 0;
        using var file = FixedArray.Read(stream, stream.Length);
        var parts = AvailableParts.Default;
        var drumsType = DrumsType.Unknown;

        var result = TestSongEntry.ParseMidiForTest(file, ref parts, ref drumsType);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasValue, Is.True);
            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Expert));
        }
    }

    private const string CHART_SONG_SECTION =
        """
        [Song]
        {
          Name = "Chart Name"
          Artist = "Someone"
          Charter = "Unknown Charter"
          Genre = "rock"
          Offset = 0.7
          PreviewStart = 30
          Difficulty = 4
          Resolution = 192
        }
        [SyncTrack]
        {
          0 = TS 4
          0 = B 120000
        }
        [ExpertSingle]
        {
          0 = N 0 0
        }
        """;

    [Test]
    public void ScanChart_TakesTextFromSongIniAloneAndTimesFromChart()
    {
        var entry = ScanChartWithIni(
            """
            [song]
            name = Test
            frets = Real Charter
            song_length = 10000
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Name.Original, Is.EqualTo("Test"));
            Assert.That(entry.Artist.Original, Is.EqualTo(SongMetadata.DEFAULT_ARTIST));
            Assert.That(entry.Charter.Original, Is.EqualTo("Real Charter"));
            Assert.That(entry.RawGenre.Original, Is.Empty);
            Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(700));
            Assert.That(entry.PreviewStartMilliseconds, Is.EqualTo(30000));
        }
    }

    [Test]
    public void ScanChart_ReadsTwoSongSectionsAsOne()
    {
        var entry = ScanChartWithIni(
            """
            [song]
            name = Test
            charter = Real Charter
            song_length = 10000
            [Song]
            preview_start_time = 0
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Name.Original, Is.EqualTo("Test"));
            Assert.That(entry.Charter.Original, Is.EqualTo("Real Charter"));
            Assert.That(entry.PreviewStartMilliseconds, Is.EqualTo(0));
        }
    }

    [TestCase("-1", 30000L)]
    [TestCase("45000", 45000L)]
    public void ScanChart_TakesPreviewStartFromChartWhenSongIniLeavesItUnset(string previewStartTime, long expected)
    {
        var entry = ScanChartWithIni(
            $"""
            [song]
            name = Test
            song_length = 10000
            preview_start_time = {previewStartTime}
            """
        );

        Assert.That(entry.PreviewStartMilliseconds, Is.EqualTo(expected));
    }

    [TestCase("-1", 4)]
    [TestCase("2", 2)]
    public void ScanChart_TakesBandDifficultyFromChartWhenSongIniLeavesItUnset(string diffBand, int expected)
    {
        var entry = ScanChartWithIni(
            $"""
            [song]
            name = Test
            song_length = 10000
            diff_band = {diffBand}
            """
        );

        Assert.That(entry.BandDifficulty, Is.EqualTo(expected));
    }

    [TestCase("", -1)]
    [TestCase("diff_band = -1", -1)]
    [TestCase("diff_band = 0", 0)]
    public void ScanChart_ReadsChartDifficultyOfZeroAsNoBandDifficulty(string diffBandLine, int expected)
    {
        var entry = ScanChartWithIni(
            $"""
            [song]
            name = Test
            song_length = 10000
            {diffBandLine}
            """,
            CHART_SONG_SECTION.Replace("Difficulty = 4", "Difficulty = 0")
        );

        Assert.That(entry.BandDifficulty, Is.EqualTo(expected));
    }

    [TestCase("0.1", 100L)]
    [TestCase("250", 250L)]
    [TestCase("0", 700L)]
    [TestCase("-1", 700L)]
    public void ScanChart_ReadsDelayWithDecimalPointAsSecondsBeforeOffset(string delay, long expected)
    {
        var entry = ScanChartWithIni(
            $"""
            [song]
            name = Test
            song_length = 10000
            delay = {delay}
            """
        );

        Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(expected));
    }

    private static UnpackedIniEntry ScanChartWithIni(string iniText, string chartText = CHART_SONG_SECTION)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"yarg-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string iniPath = Path.Combine(directory, "song.ini");
            File.WriteAllText(iniPath, iniText);
            string chartPath = Path.Combine(directory, "notes.chart");
            File.WriteAllText(chartPath, chartText);

            var result = UnpackedIniEntry.ProcessNewEntry(directory, new FileInfo(chartPath), ChartFormat.Chart,
                new FileInfo(iniPath), "");
            Assert.That(result.HasValue, Is.True, $"Expected the scan to succeed, but got {result.Error}.");
            return result.Value;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static TrackChunk MakeSyncTrack()
    {
        return new TrackChunk(
            new SequenceTrackNameEvent("TEMPO_TRACK"),
            new SetTempoEvent(TempoChange.BpmToMicroSeconds(150)),
            new TimeSignatureEvent(4, 4));
    }

    private static TrackChunk MakeGuitarTrackWithDuplicateNames()
    {
        var green = MidIOHelper.GUITAR_DIFF_START_LOOKUP[MoonDifficulty.Expert];
        var track = new TrackChunk(
            new SequenceTrackNameEvent("unrecognized track name"),
            new SequenceTrackNameEvent(MidIOHelper.GUITAR_TRACK),
            new NoteOnEvent((SevenBitNumber) (byte) green, (SevenBitNumber) (byte) MidIOHelper.VELOCITY)
            {
                DeltaTime = 10,
            },
            new NoteOffEvent((SevenBitNumber) (byte) green, (SevenBitNumber) 0)
            {
                DeltaTime = 90,
            });

        return track;
    }
}
