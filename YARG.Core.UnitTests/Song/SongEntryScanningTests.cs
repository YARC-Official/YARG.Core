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

    [Test]
    public void ParseMidi_ReadsAnEmptyTrackChunk()
    {
        var data = WriteMidi(MakeSyncTrack(), MakeGuitarTrack()).Concat(EmptyTrackChunk).ToArray();
        // Count the empty chunk in the header
        data[11] = 3;

        var (result, parts) = ParseMidi(data);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasValue, Is.True);
            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Expert));
        }
    }

    [Test]
    public void ParseMidi_StopsAfterTheCountedTracksWhenAnEmptyTrackChunkIsFollowedByEvents()
    {
        // The last counted chunk is empty, and a track's events follow it without a chunk header
        var strayEvents = new byte[] { 0x00, 0xFF, 0x03, 0x04, (byte) 'B', (byte) 'E', (byte) 'A', (byte) 'T', 0x00, 0x90, 0x0C, 0x64 };
        var data = WriteMidi(MakeSyncTrack(), MakeGuitarTrack()).Concat(EmptyTrackChunk).Concat(strayEvents).ToArray();
        data[11] = 3;

        var (result, parts) = ParseMidi(data);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasValue, Is.True);
            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Expert));
        }
    }

    [Test]
    public void ParseMidi_ReadsATrackWithoutAnEndOfTrackToTheEndOfItsChunk()
    {
        var data = WriteMidi(MakeSyncTrack(), MakeGuitarTrack());
        // Remove the guitar track's end of track (delta time 0, FF 2F 00), its last 4 bytes
        int lengthPosition = LastTrackChunkPosition(data) + 4;
        Assert.That(data[^4..], Is.EqualTo(new byte[] { 0x00, 0xFF, 0x2F, 0x00 }));
        data = data[..^4];
        data[lengthPosition + 3] -= 4;

        var (result, parts) = ParseMidi(data);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasValue, Is.True);
            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Expert));
        }
    }

    private static readonly byte[] EmptyTrackChunk = { (byte) 'M', (byte) 'T', (byte) 'r', (byte) 'k', 0x00, 0x00, 0x00, 0x00 };

    private static int LastTrackChunkPosition(byte[] data)
    {
        for (int i = data.Length - 4; i >= 0; i--)
        {
            if (data[i] == 'M' && data[i + 1] == 'T' && data[i + 2] == 'r' && data[i + 3] == 'k')
            {
                return i;
            }
        }
        throw new InvalidDataException("No MTrk chunk");
    }

    private static byte[] WriteMidi(params TrackChunk[] tracks)
    {
        var midi = new MidiFile(tracks)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
        };

        using var stream = new MemoryStream();
        midi.Write(stream);
        return stream.ToArray();
    }

    private static (ScanExpected<long> result, AvailableParts parts) ParseMidi(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var file = FixedArray.Read(stream, stream.Length);
        var parts = AvailableParts.Default;
        var drumsType = DrumsType.Unknown;

        var result = TestSongEntry.ParseMidiForTest(file, ref parts, ref drumsType);
        return (result, parts);
    }

    private static TrackChunk MakeGuitarTrack()
    {
        return MakeNoteTrack(MidIOHelper.GUITAR_TRACK, MidIOHelper.GUITAR_DIFF_START_LOOKUP[MoonDifficulty.Expert]);
    }

    private static TrackChunk MakeNoteTrack(string trackName, int noteNumber)
    {
        return new TrackChunk(
            new SequenceTrackNameEvent(trackName),
            new NoteOnEvent((SevenBitNumber) (byte) noteNumber, (SevenBitNumber) (byte) MidIOHelper.VELOCITY)
            {
                DeltaTime = 10,
            },
            new NoteOffEvent((SevenBitNumber) (byte) noteNumber, (SevenBitNumber) 0)
            {
                DeltaTime = 90,
            });
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
