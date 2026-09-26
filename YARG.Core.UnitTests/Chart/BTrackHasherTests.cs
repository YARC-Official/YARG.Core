using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;

namespace YARG.Core.UnitTests.Chart;

public class BTrackHasherTests
{
    // CHNF + version 20260801, resolution 480, one green strum at tick 0.
    // Empty list sections are omitted. Offsets: map starts at 8, two entries (44-byte map),
    // resolution payload at 52, notes payload at 56.
    private const string MinimalGuitarBTrackHex =
        "43484e46c1273501020000000100000000000000340000000000000004000000" +
        "090000000000000038000000000000001c000000e00100000100000000000000" +
        "0000000001000000000000000200000001000000";

    private const string MinimalGuitarHashInputHex =
        "0200000001000000000000000900000000000000e00100000100000000000000" +
        "0000000001000000000000000200000001000000";

    // CHNF/20260801, resolution 480, scoring phrase [0,120), pitched MIDI 60 at tick 0.
    // Sections: 1, 0x59410002, 0x59410003. Ids 4-9 omitted.
    private const string MinimalVocalsBTrackHex =
        "43484e46c127350103000000" +
        "0100000000000000480000000000000004000000" +
        "02004159000000004c0000000000000015000000" +
        "0300415900000000610000000000000020000000" +
        "e0010000" +
        "010000000000000000000000780000000000000000" +
        "0100000000000000000000003c00000000000000010000003c00000000000000";

    private const ulong VocalStarPowerId = 0x59410001;
    private const ulong VocalPhraseId = 0x59410002;
    private const ulong VocalNoteId = 0x59410003;

    [TestCase(Instrument.ProGuitar_17Fret)]
    [TestCase(Instrument.Vocals)]
    public void TryCalculateTrackHash_UnsupportedOrEmptyInstrument_ReturnsFalse(Instrument instrument)
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(CreateMinimalGuitarChart(), instrument, Difficulty.Expert, out _), Is.False);
    }

    [Test]
    public void CalculateTrackHash_MinimalFiveFretGuitar_WritesSectionalFileAndHashesStrippedInput()
    {
        var chart = CreateMinimalGuitarChart();

        var result = Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ToHex(result.BTrack), Is.EqualTo(MinimalGuitarBTrackHex));
            Assert.That(result.Hash, Is.EqualTo(HashHex(MinimalGuitarHashInputHex)));
            Assert.That(result.Hash, Does.Not.EndWith("="));
        }
    }

    [Test]
    public void CalculateTrackHash_FiveFretChordTap_SortsNotesByStandardType()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        var parent = new GuitarNote(FiveFretGuitarFret.Red, GuitarNoteType.Tap,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 10, 20);
        parent.AddChildNote(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Tap,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 10, 20));
        difficulty.Notes.Add(parent);

        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);

        var notes = Parse(Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert).BTrack).Notes;

        Assert.That(notes, Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (10, 20, 2, 4),
            (10, 20, 3, 4),
        }));
    }

    [Test]
    public void CalculateTrackHash_SixFretNote_UsesSixFretNoteTypes()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.SixFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(SixFretGuitarFret.Black1, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 0, 1));
        chart.SixFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);

        var notes = Parse(Hash(chart, Instrument.SixFretGuitar, Difficulty.Expert).BTrack).Notes;

        Assert.That(notes, Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 1, 7, 1),
        }));
    }

    [Test]
    public void CalculateTrackHash_DrumsKickAndAccentCymbal_MapsFlags()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<DrumNote>(Instrument.FourLaneDrums, Difficulty.Expert);
        var kick = new DrumNote(FourLaneDrumPad.Kick, DrumNoteType.Neutral,
            DrumNoteFlags.None, NoteFlags.None, 0, 0, isDoubleKick: true);
        kick.AddChildNote(new DrumNote(FourLaneDrumPad.GreenCymbal, DrumNoteType.Accent,
            DrumNoteFlags.None, NoteFlags.None, 0, 0));
        difficulty.Notes.Add(kick);

        chart.FourLaneDrums.AddDifficulty(Difficulty.Expert, difficulty);

        var notes = Parse(Hash(chart, Instrument.FourLaneDrums, Difficulty.Expert).BTrack).Notes;

        Assert.That(notes, Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 0, 13, 8),
            (0, 0, 17, 1056),
        }));
    }

    [Test]
    public void CalculateTrackHash_OverlappingNotesOfSameType_ResolvesOverlap()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 240, 0, 240));
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Hopo,
            GuitarNoteFlags.None, NoteFlags.None, 0, 60, 120, 60));

        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);

        var notes = Parse(Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert).BTrack).Notes;

        Assert.That(notes, Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 120, 2, 1),
            (120, 120, 2, 2),
        }));
    }

    [Test]
    public void CalculateTrackHash_OverlappingPhrases_ResolvesOverlap()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 0, 1));
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Red, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 120, 1));
        difficulty.Phrases.Add(new Phrase(PhraseType.StarPower, 0, 240, 0, 240));
        difficulty.Phrases.Add(new Phrase(PhraseType.StarPower, 0, 60, 120, 60));

        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);

        var starPower = Parse(Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert).BTrack).StarPower;

        Assert.That(starPower, Is.EqualTo(new List<(long Tick, long Length)>
        {
            (0, 120),
            (120, 120),
        }));
    }

    [Test]
    public void CalculateTrackHash_DuplicateDrumLane_KeepsLargestMutuallyExclusiveFlags()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<DrumNote>(Instrument.FourLaneDrums, Difficulty.Expert);
        difficulty.Notes.Add(new DrumNote(FourLaneDrumPad.YellowDrum, DrumNoteType.Ghost,
            DrumNoteFlags.None, NoteFlags.None, 0, 0));
        difficulty.Notes.Add(new DrumNote(FourLaneDrumPad.YellowCymbal, DrumNoteType.Accent,
            DrumNoteFlags.None, NoteFlags.None, 0, 0));

        chart.FourLaneDrums.AddDifficulty(Difficulty.Expert, difficulty);

        var notes = Parse(Hash(chart, Instrument.FourLaneDrums, Difficulty.Expert).BTrack).Notes;

        Assert.That(notes, Is.EqualTo(new List<(long Tick, long Length, uint Type, uint Flags)>
        {
            (0, 0, 15, 1056),
        }));
    }

    [Test]
    public void CalculateTrackHash_RangeShift_WritesRangeShiftSection()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 0, 1));
        difficulty.RangeShiftEvents.Add(new RangeShift(0, 1, 120, 1, 3, 5));
        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);

        var rangeShifts = Parse(Hash(chart, Instrument.FiveFretGuitar, Difficulty.Expert).BTrack).RangeShifts;

        Assert.That(rangeShifts, Is.EqualTo(new List<(long Tick, long Position, long Size)>
        {
            (120, 3, 5),
        }));
    }

    [Test]
    public void CalculateTrackHash_OmitsEmptyListSections()
    {
        var result = Hash(CreateMinimalGuitarChart(), Instrument.FiveFretGuitar, Difficulty.Expert);
        var parsed = Parse(result.BTrack);

        Assert.That(parsed.SectionIds, Is.EqualTo(new ulong[] { 1, 9 }));
    }

    [Test]
    public void CalculateTrackHash_HashMatchesIndependentlyStrippedFile()
    {
        var result = Hash(CreateMinimalGuitarChart(), Instrument.FiveFretGuitar, Difficulty.Expert);

        Assert.That(result.Hash, Is.EqualTo(HashBytes(StripForHash(result.BTrack))));
    }

    [Test]
    public void TryCalculateTrackHash_EmptyVocals_ReturnsFalse()
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(new SongChart(480), Instrument.Vocals, Difficulty.Expert, out _),
            Is.False);
    }

    [Test]
    public void TryCalculateTrackHash_VocalsNonExpert_ReturnsFalse()
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(CreateMinimalVocalsChart(), Instrument.Vocals, Difficulty.Hard, out _),
            Is.False);
    }

    [Test]
    public void CalculateTrackHash_MinimalVocals_WritesYargSectionsAndHashesAllOfThem()
    {
        var result = Hash(CreateMinimalVocalsChart(), Instrument.Vocals);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ToHex(result.BTrack), Is.EqualTo(MinimalVocalsBTrackHex));
            Assert.That(result.Hash, Is.EqualTo(HashBytes(StripForHash(result.BTrack))));
            Assert.That(result.Hash, Does.Not.EndWith("="));
        }
    }

    [Test]
    public void CalculateTrackHash_Vocals_OmitsRecognizedInstrumentSections()
    {
        var parsed = Parse(Hash(CreateMinimalVocalsChart(), Instrument.Vocals).BTrack);

        Assert.That(parsed.SectionIds, Is.EqualTo(new ulong[] { 1, VocalPhraseId, VocalNoteId }));
    }

    [Test]
    public void CalculateTrackHash_Vocals_MapsUnpitchedAndPercussion()
    {
        var chart = new SongChart(480);
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [
                CreateVocalPhrase(0, 120, false, new VocalNote(-1f, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60)),
                CreateVocalPhrase(120, 120, true, new VocalNote(-1f, 0, VocalNoteType.Percussion, 0, 0, 120, 0)),
            ], [], [], [], [])
        ], []);

        var parsed = Parse(Hash(chart, Instrument.Vocals).BTrack);

        Assert.That(parsed.VocalPhrases, Is.EqualTo(new List<(long Tick, long Length, byte IsPercussion)>
        {
            (0, 120, 0),
            (120, 120, 1),
        }));
        Assert.That(parsed.VocalNotes, Is.EqualTo(new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>
        {
            (0, 60, 2, 0, 0),
            (120, 0, 3, 0, 0),
        }));
    }

    [Test]
    public void CalculateTrackHash_Vocals_FlattensSlideChildren()
    {
        var parent = new VocalNote(60, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60);
        parent.AddChildNote(new VocalNote(64, 0, VocalNoteType.Lyric, 0.1, 0.1, 60, 60));

        var chart = new SongChart(480);
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [CreateVocalPhrase(0, 180, false, parent)], [], [], [], [])
        ], []);

        var parsed = Parse(Hash(chart, Instrument.Vocals).BTrack);

        Assert.That(parsed.VocalNotes, Is.EqualTo(new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>
        {
            (0, 60, 1, 60, 0),
            (60, 60, 1, 64, 0),
        }));
    }

    [Test]
    public void CalculateTrackHash_Vocals_OmitsEmptyPhrasesAndStarPower()
    {
        var empty = CreateVocalPhrase(0, 120, false);
        empty.PhraseParentNote.Flags = NoteFlags.StarPower;
        var sung = CreateVocalPhrase(120, 120, false, new VocalNote(60, 0, VocalNoteType.Lyric, 0, 0.1, 120, 60));
        sung.PhraseParentNote.Flags = NoteFlags.StarPower;

        var chart = new SongChart(480);
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [empty, sung], [], [], [], [])
        ], []);

        var parsed = Parse(Hash(chart, Instrument.Vocals).BTrack);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed.VocalPhrases, Is.EqualTo(new List<(long Tick, long Length, byte IsPercussion)>
            {
                (120, 120, 0),
            }));
            Assert.That(parsed.VocalStarPower, Is.EqualTo(new List<(long Tick, long Length)>
            {
                (120, 120),
            }));
        }
    }

    [Test]
    public void CalculateTrackHash_Vocals_RoundsPitchToMidi()
    {
        var chart = new SongChart(480);
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [
                CreateVocalPhrase(0, 120, false, new VocalNote(60.4f, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60)),
            ], [], [], [], [])
        ], []);

        var parsed = Parse(Hash(chart, Instrument.Vocals).BTrack);

        Assert.That(parsed.VocalNotes, Is.EqualTo(new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>
        {
            (0, 60, 1, 60, 0),
        }));
    }

    [Test]
    public void CalculateTrackHash_Harmony_CompositesParts()
    {
        var chart = new SongChart(480);
        chart.Harmony = new VocalsTrack(Instrument.Harmony, [
            new VocalsPart(true, [
                CreateVocalPhrase(0, 120, false, new VocalNote(60, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60)),
            ], [], [], [], []),
            new VocalsPart(true, [
                CreateVocalPhrase(0, 120, false, new VocalNote(64, 1, VocalNoteType.Lyric, 0, 0.1, 0, 60)),
            ], [], [], [], []),
            new VocalsPart(true, [], [], [], [], []),
        ], []);

        var parsed = Parse(Hash(chart, Instrument.Harmony).BTrack);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed.VocalPhrases, Has.Count.EqualTo(1));
            Assert.That(parsed.VocalNotes, Is.EqualTo(new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>
            {
                (0, 60, 1, 60, 0),
                (0, 60, 1, 64, 1),
            }));
        }
    }

    private static BTrackHashResult Hash(SongChart chart, Instrument instrument, Difficulty difficulty = Difficulty.Expert)
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(chart, instrument, difficulty, out var result), Is.True);
        return result;
    }

    private static SongChart CreateMinimalGuitarChart()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 0, 1));
        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);
        return chart;
    }

    private static SongChart CreateMinimalVocalsChart()
    {
        var chart = new SongChart(480);
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [
                CreateVocalPhrase(0, 120, false, new VocalNote(60, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60)),
            ], [], [], [], [])
        ], []);
        return chart;
    }

    private static VocalsPhrase CreateVocalPhrase(uint tick, uint length, bool percussion, params VocalNote[] notes)
    {
        var parent = new VocalNote(NoteFlags.None, percussion, 0, 0.1, tick, length);
        foreach (var note in notes)
        {
            parent.AddChildNote(note);
        }

        return new VocalsPhrase(0, 0.1, tick, length, parent, []);
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] FromHex(string hex)
    {
        return Convert.FromHexString(hex);
    }

    private static string HashHex(string hex)
    {
        return HashBytes(FromHex(hex));
    }

    private static string HashBytes(byte[] bytes)
    {
        return BTrackHashResult.Encode(Blake3.Hash(bytes));
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

    private static ParsedBTrack Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);

        var magic = (uint) ((reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
        var version = reader.ReadUInt32();
        var count = reader.ReadUInt32();
        var map = new List<(ulong Id, ulong Offset, uint Length)>();
        for (var i = 0; i < count; i++)
        {
            map.Add((reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt32()));
        }

        uint resolution = 0;
        var starPower = new List<(long Tick, long Length)>();
        var notes = new List<(long Tick, long Length, uint Type, uint Flags)>();
        var rangeShifts = new List<(long Tick, long Position, long Size)>();
        var vocalStarPower = new List<(long Tick, long Length)>();
        var vocalPhrases = new List<(long Tick, long Length, byte IsPercussion)>();
        var vocalNotes = new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>();
        var sectionIds = new List<ulong>();

        foreach (var entry in map)
        {
            sectionIds.Add(entry.Id);
            stream.Position = (long) entry.Offset;
            switch (entry.Id)
            {
                case 1:
                    resolution = reader.ReadUInt32();
                    break;
                case 4:
                    starPower = ReadPhrases(reader);
                    break;
                case 8:
                    rangeShifts = ReadRangeShifts(reader);
                    break;
                case 9:
                    notes = ReadNotes(reader);
                    break;
                case VocalStarPowerId:
                    vocalStarPower = ReadPhrases(reader);
                    break;
                case VocalPhraseId:
                    vocalPhrases = ReadVocalPhrases(reader);
                    break;
                case VocalNoteId:
                    vocalNotes = ReadVocalNotes(reader);
                    break;
            }
        }

        return new ParsedBTrack(magic, version, resolution, sectionIds, starPower, notes, rangeShifts,
            vocalStarPower, vocalPhrases, vocalNotes);
    }

    private static List<(long Tick, long Length)> ReadPhrases(BinaryReader reader)
    {
        var phrases = new List<(long Tick, long Length)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            phrases.Add((reader.ReadInt64(), reader.ReadInt64()));
        }
        return phrases;
    }

    private static List<(long Tick, long Position, long Size)> ReadRangeShifts(BinaryReader reader)
    {
        var rangeShifts = new List<(long Tick, long Position, long Size)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            rangeShifts.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64()));
        }
        return rangeShifts;
    }

    private static List<(long Tick, long Length, uint Type, uint Flags)> ReadNotes(BinaryReader reader)
    {
        var notes = new List<(long Tick, long Length, uint Type, uint Flags)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            notes.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadUInt32(), reader.ReadUInt32()));
        }
        return notes;
    }

    private static List<(long Tick, long Length, byte IsPercussion)> ReadVocalPhrases(BinaryReader reader)
    {
        var phrases = new List<(long Tick, long Length, byte IsPercussion)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            phrases.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadByte()));
        }
        return phrases;
    }

    private static List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)> ReadVocalNotes(BinaryReader reader)
    {
        var notes = new List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            notes.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadUInt32(), reader.ReadUInt32(),
                reader.ReadUInt32()));
        }
        return notes;
    }

    private readonly record struct ParsedBTrack(
        uint Magic,
        uint Version,
        uint Resolution,
        List<ulong> SectionIds,
        List<(long Tick, long Length)> StarPower,
        List<(long Tick, long Length, uint Type, uint Flags)> Notes,
        List<(long Tick, long Position, long Size)> RangeShifts,
        List<(long Tick, long Length)> VocalStarPower,
        List<(long Tick, long Length, byte IsPercussion)> VocalPhrases,
        List<(long Tick, long Length, uint Kind, uint Pitch, uint Part)> VocalNotes);
}
