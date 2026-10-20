using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using System.Text;
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

    private static readonly Instrument[] FourLane = { Instrument.FourLaneDrums };
    private static readonly Instrument[] Pro = { Instrument.FourLaneDrums, Instrument.ProDrums };
    private static readonly Instrument[] FiveLane = { Instrument.FiveLaneDrums };

    [Test]
    public void ScanChart_ReadsFalseFiveLaneDrumsAsNoKey()
    {
        var entry = ScanChartFile("five_lane_drums = False", """
            [ExpertDrums]
            {
              0 = N 1 0
              192 = N 5 0
            }
            """);

        Assert.That(OfferedDrums(entry), Is.EqualTo(FiveLane));
    }

    [Test]
    public void ScanChart_ReadsChartWithCymbalMarkerAfterFiveLaneNoteAsPro()
    {
        var entry = ScanChartFile("", """
            [ExpertDrums]
            {
              0 = N 5 0
              192 = N 2 0
              192 = N 66 0
            }
            """);

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_ReadsChartWithProDrumsAndFiveLaneNoteAsPro()
    {
        var entry = ScanChartFile("pro_drums = True", """
            [ExpertDrums]
            {
              0 = N 5 0
              192 = N 2 0
            }
            """);

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_CountsFiveLaneNoteOfProChartAsNote()
    {
        var entry = ScanChartFile("", """
            [ExpertDrums]
            {
              0 = N 2 0
              0 = N 66 0
            }
            [HardDrums]
            {
              0 = N 5 0
            }
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
            Assert.That(entry[Instrument.ProDrums].Difficulties, Is.EqualTo(DifficultyMask.Hard | DifficultyMask.Expert));
        }
    }

    [Test]
    public void ScanChart_ReadsMidWithoutTomMarkersOrProDrumsAsFourLane()
    {
        var entry = ScanMidFile("", Note(0, 97), Note(192, 98), Note(384, 99), Note(576, 100));

        Assert.That(OfferedDrums(entry), Is.EqualTo(FourLane));
    }

    [Test]
    public void ScanChart_ReadsMidWithFalseFiveLaneDrumsAndFiveLaneNoteAsFiveLane()
    {
        var entry = ScanMidFile("five_lane_drums = False", Note(0, 101), Note(192, 98));

        Assert.That(OfferedDrums(entry), Is.EqualTo(FiveLane));
    }

    [Test]
    public void ScanChart_ReadsMidWithProDrumsAndFiveLaneNoteAsPro()
    {
        var entry = ScanMidFile("pro_drums = True", Note(0, 101), Note(192, 98));

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_ReadsMidWithTomMarkerAfterFiveLaneNoteAsPro()
    {
        var entry = ScanMidFile("", Note(0, 101), Note(192, 98), Note(192, 110));

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_CountsFiveLaneNoteOfProMidAsNote()
    {
        // 89 is Hard's 5-lane green
        var entry = ScanMidFile("", Note(0, 98), Note(0, 110), Note(192, 89));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
            Assert.That(entry[Instrument.ProDrums].Difficulties, Is.EqualTo(DifficultyMask.Hard | DifficultyMask.Expert));
        }
    }

    [Test]
    public void ScanChart_ReadsMidWithTomMarkerThatCoversNoNoteAsFourLane()
    {
        var entry = ScanMidFile("", Note(0, 98), Note(384, 110));

        Assert.That(OfferedDrums(entry), Is.EqualTo(FourLane));
    }

    [Test]
    public void ScanChart_TomMarkerDoesNotCoverNoteOnItsEndTick()
    {
        // The note at 192 starts before the marker ends in the file, but on the marker's end tick
        var entry = ScanMidFile("", On(0, 110), On(192, 98), Off(192, 110), Off(240, 98));

        Assert.That(OfferedDrums(entry), Is.EqualTo(FourLane));
    }

    [Test]
    public void ScanChart_TomMarkerCoversKick()
    {
        var entry = ScanMidFile("", Note(0, 96), Note(0, 110), Note(192, 98));

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_TomMarkerOfLengthZeroCoversNoteAfterItOnItsTick()
    {
        var entry = ScanMidFile("", On(0, 110), Off(0, 110), On(0, 98), Off(192, 98));

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    [Test]
    public void ScanChart_DrumsTrackAfterEliteDrumsTrackSetsDrumType()
    {
        // 76 is an Expert Elite Drums note
        var entry = ScanMidFile("",
            MakeNoteTrack(MidIOHelper.ELITE_DRUMS_TRACK, Note(0, 76)),
            MakeNoteTrack(MidIOHelper.DRUMS_TRACK, Note(0, 101), Note(192, 98)));

        Assert.That(OfferedDrums(entry), Is.EqualTo(FiveLane));
    }

    [Test]
    public void ScanChart_ReadsEliteDrumsDownchartWithoutDrumsTrackAsPro()
    {
        var entry = ScanMidFile("", MakeNoteTrack(MidIOHelper.ELITE_DRUMS_TRACK, Note(0, 76)));

        Assert.That(OfferedDrums(entry), Is.EqualTo(Pro));
    }

    private static Instrument[] OfferedDrums(SongEntry entry)
    {
        return new[] { Instrument.FourLaneDrums, Instrument.ProDrums, Instrument.FiveLaneDrums }
            .Where(entry.HasInstrument)
            .ToArray();
    }

    private static SongEntry ScanChartFile(string iniKeys, string drumSections)
    {
        string chart = $$"""
            [Song]
            {
              Resolution = {{Resolution}}
            }
            [SyncTrack]
            {
              0 = TS 4
              0 = B 120000
            }
            {{drumSections}}
            """;
        return ScanSongFolder("notes.chart", ChartFormat.Chart, Encoding.UTF8.GetBytes(chart), iniKeys);
    }

    private readonly record struct DrumEvent(long Tick, int Note, bool IsOn);

    private static DrumEvent[] Note(long tick, int note)
    {
        return new[] { new DrumEvent(tick, note, true), new DrumEvent(tick + Resolution / 4, note, false) };
    }

    private static DrumEvent[] On(long tick, int note) => new[] { new DrumEvent(tick, note, true) };

    private static DrumEvent[] Off(long tick, int note) => new[] { new DrumEvent(tick, note, false) };

    private static SongEntry ScanMidFile(string iniKeys, params DrumEvent[][] drumEvents)
    {
        return ScanMidFile(iniKeys, MakeNoteTrack(MidIOHelper.DRUMS_TRACK, drumEvents));
    }

    private static SongEntry ScanMidFile(string iniKeys, params TrackChunk[] tracks)
    {
        var midi = new MidiFile(tracks.Prepend(MakeSyncTrack()))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
        };
        using var stream = new MemoryStream();
        midi.Write(stream);
        return ScanSongFolder("notes.mid", ChartFormat.Mid, stream.ToArray(), iniKeys);
    }

    // Writes the events in the order given at each tick
    private static TrackChunk MakeNoteTrack(string name, params DrumEvent[][] noteEvents)
    {
        var track = new TrackChunk(new SequenceTrackNameEvent(name));
        long previousTick = 0;
        foreach (var (tick, note, isOn) in noteEvents.SelectMany(events => events).OrderBy(item => item.Tick))
        {
            MidiEvent midiEvent = isOn
                ? new NoteOnEvent((SevenBitNumber) (byte) note, (SevenBitNumber) 100)
                : new NoteOffEvent((SevenBitNumber) (byte) note, (SevenBitNumber) 0);
            midiEvent.DeltaTime = tick - previousTick;
            previousTick = tick;
            track.Events.Add(midiEvent);
        }
        return track;
    }

    private static SongEntry ScanSongFolder(string chartName, ChartFormat format, byte[] chart, string iniKeys)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"yarg-drums-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string chartPath = Path.Combine(directory, chartName);
            File.WriteAllBytes(chartPath, chart);
            string iniPath = Path.Combine(directory, "song.ini");
            File.WriteAllText(iniPath, $"[song]\nname = Drums\nsong_length = 1000\n{iniKeys}\n");

            var result = UnpackedIniEntry.ProcessNewEntry(directory, new FileInfo(chartPath), format, new FileInfo(iniPath), "");
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
