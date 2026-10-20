using System.Text.Json;
using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Chart.Hashing;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Chart;

/// <summary>
/// Runs scan-chart's golden vectors through YARG's scan, chart load and <see cref="BTrackHasher"/>: each vector's
/// song folder in, each track's TrackHash out.
/// </summary>
/// <remarks>
/// ScanChartGoldenVectors holds the source and song vectors of scan-chart's suite v4 (vectors.json names the commit):
/// each vector's input files, and each track's TrackHash and BTrack. A source vector is a notes file and its chart
/// modifiers, so its folder also has a song.ini with a name, which YARG needs to list a song, and the modifiers that
/// aren't defaults.
/// </remarks>
public class ScanChartGoldenVectorTests
{
    private static readonly string VectorsDirectory = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "../../../../Chart/ScanChartGoldenVectors"));

    // Vectors, or one track of a vector, that YARG reads differently from scan-chart, outside the hasher. Each is
    // skipped while YARG's TrackHash differs, and fails once it matches, so that its entry is removed.
    private const string MidDrumType = "YARG reads a .mid without pro_drums as pro drums";
    private const string NoName = "YARG lists no song without a name";
    private const string SameTickMixEvents = "Of two mix events on one tick, YARG uses the later";
    private const string NoChordSnap = "YARG doesn't read chord_snap_threshold";
    private static readonly Dictionary<string, string> KnownDifferences = new()
    {
        ["source-chart-range-shifts-tempo"] = "YARG reads a range shift of size 0",
        ["source-midi-modifier-boundaries"] = "YARG doesn't tap a note on a tap's end tick, and reads a range shift of size 0",
        ["source-midi-all-difficulty-tap-boundary"] = "YARG doesn't tap a note on a tap's end tick",
        ["source-midi-late-chart-dynamics-expert"] = MidDrumType,
        ["source-midi-late-chart-dynamics-hard"] = MidDrumType,
        ["source-midi-chart-dynamics-before-same-tick-note"] = MidDrumType,
        ["source-midi-chart-dynamics-after-same-tick-note"] = MidDrumType +
            ", and ENABLE_CHART_DYNAMICS also covers the notes listed before it on its tick",
        ["source-midi-kick-dynamics-ignored"] = MidDrumType,
        ["source-midi-tom-marker-covering-no-notes"] = "YARG reads a tom marker that covers no note as pro drums",
        ["source-chart-flam"] = "YARG doesn't read .chart flams (N 109)",
        ["source-midi-disco-d-dnoflip-off"] = MidDrumType,
        ["source-midi-disco-d-dnoflip-without-off"] = MidDrumType,
        ["source-midi-disco-dnoflip-d-off"] = MidDrumType,
        ["source-chart-disco-same-tick-d-and-dnoflip"] = SameTickMixEvents,
        ["source-midi-disco-same-tick-d-and-dnoflip"] = SameTickMixEvents + ". " + MidDrumType,
        ["source-chart-disco-same-tick-off"] = SameTickMixEvents,
        ["source-midi-disco-same-tick-off"] = SameTickMixEvents + ". " + MidDrumType,
        ["source-midi-range-shifts"] = "YARG reads range shifts that aren't FF 01 text events, and keeps two on one tick",
        ["song-minimal-chart-without-ini"] = NoName,
        ["song-midi-track-name-case"] = "YARG reads track names only in capitals, and doesn't read PART DRUM",
        ["song-midi-tick-zero-track-names guitar"] = "Of several track names at tick 0, YARG uses the first it knows",
        ["song-folder-both-notes-files"] = MidDrumType,
        ["song-ghl-and-keysghl-members"] = NoName + ", and doesn't read 6-fret keys",
        ["song-ini-modifiers-chart-integers"] = NoChordSnap,
        ["song-ini-modifiers-mid-signed-integers"] = NoChordSnap,
        ["song-ini-modifiers-invalid-integers"] = "YARG reads a number's leading digits, so 170.5 is 170 and 103.0 is 103",
        ["song-ini-modifiers-booleans"] = "YARG reads a boolean that doesn't start with 1 or true as false",
        ["song-duration-tempo-change-in-added-beat"] = NoName,
        ["song-duration-latest-note-across-tracks"] = NoName,
        ["song-duration-rounding-and-track-minimum"] = NoName,
        ["song-flam-chart"] = "YARG doesn't read .chart flams (N 109)",
        ["song-flam-mid"] = "YARG doesn't read flams (note 109). " + MidDrumType,
    };

    // The scan measures the audio of a song whose song.ini has no song_length, and the vectors have no audio
    [OneTimeSetUp]
    public void SetUp() => GlobalAudioHandler.Initialize<NoAudioManager>();

    [OneTimeTearDown]
    public void TearDown() => GlobalAudioHandler.Close();

    private static IEnumerable<TestCaseData> Tracks()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(VectorsDirectory, "vectors.json")));
        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var id = vector.GetProperty("id").GetString()!;
            foreach (var track in vector.GetProperty("tracks").EnumerateArray())
            {
                var instrument = track.GetProperty("instrument").GetString()!;
                var difficulty = track.GetProperty("difficulty").GetString()!;
                yield return new TestCaseData(id, instrument, difficulty, track.GetProperty("trackHash").GetString()!,
                    track.GetProperty("bTrack").GetString()!).SetArgDisplayNames(id, instrument, difficulty);
            }
        }
    }

    [TestCaseSource(nameof(Tracks))]
    public void TrackHash_MatchesScanChart(string id, string instrument, string difficulty, string trackHash,
        string bTrack)
    {
        var failure = TryHash(Path.Combine(VectorsDirectory, id), instrument, difficulty, out var result);
        if (KnownDifferences.TryGetValue($"{id} {instrument}", out var reason) || KnownDifferences.TryGetValue(id, out reason))
        {
            Assert.That(failure is null && result.Hash == trackHash, Is.False,
                $"{id} {instrument} matches scan-chart now: remove it from {nameof(KnownDifferences)}");
            Assert.Ignore($"{reason}\n{failure ?? DescribeDifference(bTrack, result.BTrack)}");
        }

        Assert.That(failure, Is.Null);
        Assert.That(result.Hash, Is.EqualTo(trackHash), DescribeDifference(bTrack, result.BTrack));
    }

    /// <returns>Why YARG gives the track no TrackHash, or null when it gives one</returns>
    private static string? TryHash(string directory, string instrument, string difficulty, out BTrackHashResult result)
    {
        result = default;
        var chart = IniSubEntry.CHART_FILE_TYPES.First(type => File.Exists(Path.Combine(directory, type.Filename)));
        var ini = new FileInfo(Path.Combine(directory, "song.ini"));
        var scan = UnpackedIniEntry.ProcessNewEntry(directory, new FileInfo(Path.Combine(directory, chart.Filename)),
            chart.Format, ini.Exists ? ini : null, string.Empty);
        if (!scan.HasValue)
        {
            return $"YARG's scan gives {scan.Error}";
        }

        var yargInstrument = MapInstrument(scan.Value, instrument);
        if (yargInstrument is null)
        {
            return $"YARG has no instrument for {instrument}";
        }

        return scan.Value.TryGetBTrackHash(yargInstrument.Value, MapDifficulty(difficulty), out result)
            ? null
            : $"YARG gives {yargInstrument} {difficulty} no TrackHash";
    }

    // scan-chart has one drums track per chart, which keeps its cymbals: the chart's own drum type in YARG
    private static Instrument? MapInstrument(SongEntry entry, string instrument)
    {
        return instrument switch
        {
            "guitar"        => Instrument.FiveFretGuitar,
            "guitarcoop"    => Instrument.FiveFretCoopGuitar,
            "rhythm"        => Instrument.FiveFretRhythm,
            "bass"          => Instrument.FiveFretBass,
            "keys"          => Instrument.Keys,
            "guitarghl"     => Instrument.SixFretGuitar,
            "guitarcoopghl" => Instrument.SixFretCoopGuitar,
            "rhythmghl"     => Instrument.SixFretRhythm,
            "bassghl"       => Instrument.SixFretBass,
            "drums" => entry.HasInstrument(Instrument.ProDrums) ? Instrument.ProDrums
                : entry.HasInstrument(Instrument.FiveLaneDrums) ? Instrument.FiveLaneDrums
                : Instrument.FourLaneDrums,
            _ => null,
        };
    }

    private static Difficulty MapDifficulty(string difficulty)
    {
        return difficulty switch
        {
            "easy"   => Difficulty.Easy,
            "medium" => Difficulty.Medium,
            "hard"   => Difficulty.Hard,
            "expert" => Difficulty.Expert,
            _ => throw new ArgumentException($"Unknown difficulty {difficulty}"),
        };
    }

    private static string DescribeDifference(string expectedBase64, byte[] actual)
    {
        var scanChart = DecodeRecords(Convert.FromBase64String(expectedBase64));
        var yarg = DecodeRecords(actual);
        return string.Join("\n", scanChart.Except(yarg).Select(record => $"scan-chart only: {record}")
            .Concat(yarg.Except(scanChart).Select(record => $"YARG only: {record}")));
    }

    private static List<string> DecodeRecords(byte[] bTrack)
    {
        var records = new List<string>();
        using var reader = new BinaryReader(new MemoryStream(bTrack));
        reader.BaseStream.Position = 8;
        var sections = new List<(ulong Id, ulong Offset)>();
        var count = reader.ReadUInt32();
        for (var i = 0; i < count; i++)
        {
            sections.Add((reader.ReadUInt64(), reader.ReadUInt64()));
            reader.ReadUInt32();
        }

        foreach (var (id, offset) in sections)
        {
            reader.BaseStream.Position = (long) offset;
            if (id == 1)
            {
                records.Add($"resolution {reader.ReadUInt32()}");
                continue;
            }

            var recordCount = id <= 9 ? reader.ReadUInt32() : 0;
            for (var i = 0; i < recordCount; i++)
            {
                records.Add(id switch
                {
                    2 => $"tempo at {reader.ReadInt64()}: {reader.ReadDouble():R} BPM",
                    3 => $"time signature at {reader.ReadInt64()}: {reader.ReadUInt32()}/{reader.ReadUInt32()}",
                    4 => $"star power at {reader.ReadInt64()}, length {reader.ReadInt64()}",
                    5 => $"solo at {reader.ReadInt64()}, length {reader.ReadInt64()}",
                    6 => $"lane at {reader.ReadInt64()}, length {reader.ReadInt64()}, double {reader.ReadByte()}",
                    7 => $"drum fill at {reader.ReadInt64()}, length {reader.ReadInt64()}, coda {reader.ReadByte()}",
                    8 => $"range shift at {reader.ReadInt64()}: position {reader.ReadInt64()}, size {reader.ReadInt64()}",
                    _ => $"note at {reader.ReadInt64()}, length {reader.ReadInt64()}: type {reader.ReadUInt32()}, flags {reader.ReadUInt32()}",
                });
            }
        }

        return records;
    }

    private sealed class NoAudioManager : AudioManager
    {
        protected internal override ReadOnlySpan<string> SupportedFormats => [];
        protected internal override StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume, bool normalize) => null;
        protected internal override List<InputDeviceInfo> GetAllInputDevices() => [];
        protected internal override MicDevice? CreateInputDevice(InputDeviceInfo device) => null;
        protected internal override OutputChannel? CreateOutputChannel(int channelId) => null;
        protected internal override List<(int id, string name)> GetAllOutputDevices() => [];
        protected internal override int GetOutputChannelCount() => 0;
        protected internal override bool SetOutputDevice(string name) => false;
        protected internal override void SetMasterVolume(double volume) { }
        public override void LoadVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel = null) { }
        public override void ClearVenueSamples() { }
        public override PushStream CreatePushStream(int sampleRate, int channelCount) => throw new NotSupportedException();
        protected internal override void PlayMetronomeSoundEffectToChannel(MetronomeSample sample, MetronomePitch pitch, int channelId) { }
        protected override void SetBufferLength_Internal(int length) { }
    }
}
