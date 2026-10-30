using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song;
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
