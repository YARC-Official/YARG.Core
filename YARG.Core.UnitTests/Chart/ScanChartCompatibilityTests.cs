using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;
using YARG.Core.Parsing;
using YARG.Core.UnitTests.Parsing;

namespace YARG.Core.UnitTests.Chart;

public class ScanChartCompatibilityTests
{
    [Test]
    public void DotChartGuitar_OverlapNotes_ResolvesSameTypeOverlap()
    {
        var result = CalculateDotChartHash("ExpertSingle", Instrument.FiveFretGuitar,
            "0 = N 0 240",
            "120 = N 0 60");

        Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 120, 2, 1),
            (120, 120, 2, 1),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void DotChartGuitar_OverlapStarPower_ResolvesPhraseOverlap()
    {
        var result = CalculateDotChartHash("ExpertSingle", Instrument.FiveFretGuitar,
            "0 = S 2 240",
            "0 = N 0 0",
            "120 = S 2 60",
            "120 = N 1 0");

        Assert.That(ReadSectionPhrases(result.BTrack, 4), Is.EqualTo(new List<(long Tick, long Length)>
        {
            (0, 120),
            (120, 120),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void DotChartGuitar_SameFretAfterChord_IsNaturalHopoOnChart()
    {
        var result = CalculateDotChartHash("ExpertSingle", Instrument.FiveFretGuitar,
            "0 = N 0 0",
            "0 = N 1 0",
            "120 = N 0 0");

        Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 0, 2, 1),
            (0, 0, 3, 1),
            (120, 0, 2, 2),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void DotChartGuitar_OutOfOrderAndMalformedLines_StillParsesValidNotes()
    {
        var result = CalculateDotChartHash("ExpertSingle", Instrument.FiveFretGuitar,
            "240 = N 2 0",
            "malformed chart line",
            "0 = N 0 0",
            "120 = N 1 0");

        Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 0, 2, 1),
            (120, 0, 3, 2),
            (240, 0, 4, 2),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void DotChartGuitar_PhrasesForcesAndTaps_MapsModifiers()
    {
        var result = CalculateDotChartHash("ExpertSingle", Instrument.FiveFretGuitar,
            "0 = S 2 480",
            "0 = E \"solo\"",
            "0 = N 0 0",
            "120 = N 1 0",
            "120 = N 5 0",
            "240 = N 2 0",
            "240 = N 6 0",
            "480 = E \"soloend\"");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadSectionPhrases(result.BTrack, 4), Is.EqualTo(new List<(long Tick, long Length)>
            {
                (0, 480),
            }));
            Assert.That(ReadSectionPhrases(result.BTrack, 5), Is.EqualTo(new List<(long Tick, long Length)>
            {
                (0, 481),
            }));
            Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
            {
                (0, 0, 2, 1),
                (120, 0, 3, 1),
                (240, 0, 4, 4),
            }));
        }
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void DotChartDrums_KickAndCymbal_MapsDrumTypes()
    {
        var result = CalculateDotChartHash("ExpertDrums", Instrument.FourLaneDrums,
            "0 = N 0 0",
            "0 = N 4 0",
            "0 = N 66 0",
            "0 = N 34 0");

        Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 0, 13, 0),
            (0, 0, 17, 16),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void MidiGuitar_StarPowerAndNotes_HashesStrippedSectionalFile()
    {
        var midi = new MidiFile(
            new TrackChunk(new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
            new TrackChunk(
                new SequenceTrackNameEvent("PART GUITAR"),
                NoteOn(0, 96),
                NoteOn(0, 116),
                NoteOff(120, 96),
                NoteOn(0, 97),
                NoteOff(240, 116),
                NoteOff(0, 97)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(128),
        };

        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, midi);
        var result = Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert);

        Assert.That(ReadNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 120, 2, 1),
            (120, 240, 3, 1),
        }));
        Assert.That(ReadSectionPhrases(result.BTrack, 4), Is.EqualTo(new List<(long Tick, long Length)>
        {
            (0, 360),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void MidiGuitar_Solo_DoesNotAddTerminalTick()
    {
        var midi = new MidiFile(
            new TrackChunk(new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
            new TrackChunk(
                new SequenceTrackNameEvent("PART GUITAR"),
                NoteOn(0, 103),
                NoteOn(0, 96),
                NoteOff(240, 96),
                NoteOff(0, 103)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480),
        };

        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, midi);
        var result = Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert);

        Assert.That(ReadSectionPhrases(result.BTrack, 5), Is.EqualTo(new List<(long Tick, long Length)>
        {
            (0, 240),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void MidiDrums_CodaFill_StaysFreestyleAndMarksCoda()
    {
        var midi = new MidiFile(
            new TrackChunk(new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
            new TrackChunk(
                new SequenceTrackNameEvent("EVENTS"),
                new Melanchall.DryWetMidi.Core.TextEvent("[coda]") { DeltaTime = 100 }),
            new TrackChunk(
                new SequenceTrackNameEvent("PART DRUMS"),
                NoteOn(0, 120),
                NoteOff(80, 120),
                NoteOn(20, 120),
                NoteOff(40, 120)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480),
        };

        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, midi);
        var result = Hash(chart, Instrument.FourLaneDrums, Difficulty.Expert);

        Assert.That(ReadDrumFreestyles(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, byte IsCoda)>
        {
            (0, 80, 0),
            (100, 40, 1),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void MidiVocals_StarPowerBeforePhrase_UsesScoringPhrase()
    {
        var midi = new MidiFile(
            new TrackChunk(new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
            new TrackChunk(
                new SequenceTrackNameEvent("PART VOCALS"),
                NoteOn(0, 116),
                NoteOn(50, 105),
                NoteOn(0, 60),
                NoteOff(100, 60),
                NoteOff(0, 105),
                NoteOff(50, 116)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480),
        };

        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, midi);
        var result = Hash(chart, Instrument.Vocals, Difficulty.Expert);

        Assert.That(ReadSectionPhrases(result.BTrack, 0x59410001), Is.EqualTo(new List<(long Tick, long Length)>
        {
            (50, 100),
        }));
        AssertHashMatchesStrippedFile(result);
    }

    [Test]
    public void MidiVocals_PhrasePitchAndStarPower_UsesYargSections()
    {
        var midi = new MidiFile(
            new TrackChunk(new SetTempoEvent(TempoChange.BpmToMicroSeconds(120))),
            new TrackChunk(
                new SequenceTrackNameEvent("PART VOCALS"),
                NoteOn(0, 105),
                NoteOn(0, 60),
                NoteOn(0, 116),
                NoteOff(60, 60),
                NoteOff(60, 116),
                NoteOff(0, 105)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480),
        };

        var chart = SongChart.FromMidi(ParseSettings.Default_Midi, midi);
        var result = Hash(chart, Instrument.Vocals, Difficulty.Expert);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadSectionIds(result.BTrack), Does.Not.Contain(4ul));
            Assert.That(ReadSectionIds(result.BTrack), Does.Not.Contain(9ul));
            Assert.That(ReadSectionPhrases(result.BTrack, 0x59410001), Is.EqualTo(new List<(long Tick, long Length)>
            {
                (0, 120),
            }));
            Assert.That(ReadVocalPhrases(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, byte IsPercussion)>
            {
                (0, 120, 0),
            }));
            Assert.That(ReadVocalNotes(result.BTrack), Is.EqualTo(new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>
            {
                (0, 60, 1, 60, 0),
            }));
        }
        AssertHashMatchesStrippedFile(result);
    }

    private static void AssertHashMatchesStrippedFile(BTrackHashResult result)
    {
        Assert.That(result.Hash, Is.EqualTo(BTrackHashResult.Encode(Blake3.Hash(StripForHash(result.BTrack)))));
        Assert.That(result.Hash, Does.Not.EndWith("="));
    }

    private static BTrackHashResult Hash(SongChart chart, Instrument instrument, Difficulty difficulty = Difficulty.Expert)
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(chart, instrument, difficulty, out var result), Is.True);
        return result;
    }

    private static BTrackHashResult CalculateDotChartHash(string trackName, Instrument instrument, params string[] trackLines)
    {
        var chartText = ChartText.Chart(
            ChartText.SongSection(480),
            ChartText.SyncSection(),
            ChartText.Section("Events"),
            ChartText.Section(trackName, trackLines));

        var chart = SongChart.FromDotChart(ParseSettings.Default_Chart, chartText.AsSpan());
        return Hash(chart, instrument, Difficulty.Expert);
    }

    private static byte[] StripForHash(byte[] bTrack)
    {
        using var stream = new MemoryStream(bTrack);
        using var reader = new BinaryReader(stream);
        stream.Position = 8;
        var count = reader.ReadUInt32();
        var ids = new List<ulong>();
        var payloads = new List<byte[]>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(reader.ReadUInt64());
            var offset = reader.ReadUInt64();
            var length = reader.ReadUInt32();
            var restore = stream.Position;
            stream.Position = (long) offset;
            payloads.Add(reader.ReadBytes((int) length));
            stream.Position = restore;
        }

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write((uint) ids.Count);
        foreach (var id in ids)
        {
            writer.Write(id);
        }
        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }

        return output.ToArray();
    }

    private static List<(long Tick, long Length, uint Type, uint Flags)> ReadNotes(byte[] bTrack)
    {
        return ReadSection(bTrack, 9, reader =>
        {
            var notes = new List<(long Tick, long Length, uint Type, uint Flags)>();
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                notes.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadUInt32(), reader.ReadUInt32()));
            }
            return notes;
        }) ?? new List<(long Tick, long Length, uint Type, uint Flags)>();
    }

    private static List<ulong> ReadSectionIds(byte[] bTrack)
    {
        using var stream = new MemoryStream(bTrack);
        using var reader = new BinaryReader(stream);
        stream.Position = 8;
        var count = reader.ReadUInt32();
        var ids = new List<ulong>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(reader.ReadUInt64());
            reader.ReadUInt64();
            reader.ReadUInt32();
        }

        return ids;
    }

    private static List<(long Tick, long Length, byte IsPercussion)> ReadVocalPhrases(byte[] bTrack)
    {
        return ReadSection(bTrack, 0x59410002, reader =>
        {
            var phrases = new List<(long Tick, long Length, byte IsPercussion)>();
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                phrases.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadByte()));
            }
            return phrases;
        }) ?? new List<(long Tick, long Length, byte IsPercussion)>();
    }

    private static List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)> ReadVocalNotes(byte[] bTrack)
    {
        return ReadSection(bTrack, 0x59410003, reader =>
        {
            var notes = new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>();
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                notes.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadUInt32(), reader.ReadUInt32(),
                    reader.ReadUInt32()));
            }
            return notes;
        }) ?? new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>();
    }

    private static List<(long Tick, long Length, byte IsCoda)> ReadDrumFreestyles(byte[] bTrack)
    {
        return ReadSection(bTrack, 7, reader =>
        {
            var phrases = new List<(long Tick, long Length, byte IsCoda)>();
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                phrases.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadByte()));
            }
            return phrases;
        }) ?? new List<(long Tick, long Length, byte IsCoda)>();
    }

    private static List<(long Tick, long Length)> ReadSectionPhrases(byte[] bTrack, ulong sectionId)
    {
        return ReadSection(bTrack, sectionId, reader =>
        {
            var phrases = new List<(long Tick, long Length)>();
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                phrases.Add((reader.ReadInt64(), reader.ReadInt64()));
            }
            return phrases;
        }) ?? new List<(long Tick, long Length)>();
    }

    private static T? ReadSection<T>(byte[] bTrack, ulong sectionId, Func<BinaryReader, T> read)
    {
        using var stream = new MemoryStream(bTrack);
        using var reader = new BinaryReader(stream);
        stream.Position = 8;
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadUInt64();
            var offset = reader.ReadUInt64();
            reader.ReadUInt32();
            if (id != sectionId)
            {
                continue;
            }

            stream.Position = (long) offset;
            return read(reader);
        }

        return default;
    }

    private static NoteOnEvent NoteOn(long delta, int note)
    {
        return new NoteOnEvent
        {
            DeltaTime = delta,
            NoteNumber = (SevenBitNumber) note,
            Velocity = (SevenBitNumber) 100,
        };
    }

    private static NoteOffEvent NoteOff(long delta, int note)
    {
        return new NoteOffEvent
        {
            DeltaTime = delta,
            NoteNumber = (SevenBitNumber) note,
        };
    }
}
