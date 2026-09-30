using System.Text.Json;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Chart;

public class ScanChartGoldenVectorTests
{
    private static readonly string? ScanChartRoot = FindScanChartRoot();
    private static readonly string? SuiteRoot = ScanChartRoot is null
        ? null
        : Path.Combine(ScanChartRoot, "test", "golden-vectors", "suites", "v2");

    [Test]
    [Explicit("Compares against sibling scan-chart yarg-btrack-20260801-preview v2 goldens.")]
    public void Canonical20260801_SourceVectors_MatchScanChartPreview()
    {
        if (SuiteRoot is null)
        {
            Assert.Ignore("scan-chart golden vectors not found. Set SCAN_CHART_ROOT or keep scan-chart next to YARG.Core.");
        }

        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SuiteRoot, "manifest.json")));
        var mismatches = new List<string>();

        foreach (var vector in manifest.RootElement.GetProperty("vectors").EnumerateArray())
        {
            if (vector.GetProperty("kind").GetString() != "source-accepted")
            {
                continue;
            }

            var id = vector.GetProperty("id").GetString()!;
            var format = vector.GetProperty("sourceFormat").GetString()!;
            var sourcePath = Path.Combine(SuiteRoot, vector.GetProperty("source").GetProperty("path").GetString()!);
            var instrumentName = vector.GetProperty("target").GetProperty("instrument").GetString()!;
            var difficultyName = vector.GetProperty("target").GetProperty("difficulty").GetString()!;
            var expected = vector.GetProperty("expected").GetProperty("canonical20260801");
            var expectedBTrackPath = Path.Combine(SuiteRoot, expected.GetProperty("physicalBTrack").GetProperty("path").GetString()!);
            var expectedHash = expected.GetProperty("trackHash").GetString()!;
            var expectedProjectionPath = Path.Combine(SuiteRoot, expected.GetProperty("projection").GetProperty("path").GetString()!);

            var instrument = MapInstrument(instrumentName);
            var difficulty = MapDifficulty(difficultyName);
            var settings = format == "mid" ? ParseSettings.Default_Midi : ParseSettings.Default_Chart;
            ApplyModifiers(ref settings, vector.GetProperty("modifiers"));

            var chart = format == "mid"
                ? SongChart.FromMidi(settings, Melanchall.DryWetMidi.Core.MidiFile.Read(sourcePath))
                : SongChart.FromDotChart(settings, File.ReadAllText(sourcePath).AsSpan());

            if (!BTrackHasher.TryCalculateTrackHash(chart, instrument, difficulty, out var result))
            {
                mismatches.Add($"{id}: YARG could not hash {instrumentName}/{difficultyName}");
                continue;
            }

            var expectedBTrack = File.ReadAllBytes(expectedBTrackPath);
            var expectedProjection = File.ReadAllBytes(expectedProjectionPath);
            var stages = new List<string>();
            if (!result.BTrack.AsSpan().SequenceEqual(expectedBTrack))
            {
                stages.Add($"physical.btrack yarg={result.BTrack.Length}B scan={expectedBTrack.Length}B firstDiff={FirstDiff(result.BTrack, expectedBTrack)}");
            }

            if (result.Hash != expectedHash)
            {
                stages.Add($"trackHash yarg={result.Hash} scan={expectedHash}");
            }

            if (stages.Count > 0)
            {
                mismatches.Add($"{id} ({format} {instrumentName}/{difficultyName}): {string.Join("; ", stages)}");
            }

            _ = expectedProjection;
        }

        Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
    }

    [Test]
    [Explicit("Compares against sibling scan-chart yarg-btrack-20260801-preview v2 goldens.")]
    public void Canonical20260801_ProjectionDigests_MatchYargBlake3()
    {
        if (SuiteRoot is null)
        {
            Assert.Ignore("scan-chart golden vectors not found. Set SCAN_CHART_ROOT or keep scan-chart next to YARG.Core.");
        }

        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SuiteRoot, "manifest.json")));
        var mismatches = new List<string>();
        var checkedCount = 0;

        foreach (var vector in manifest.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var kind = vector.GetProperty("kind").GetString();
            JsonElement expected;
            if (kind == "source-accepted")
            {
                expected = vector.GetProperty("expected").GetProperty("canonical20260801");
            }
            else if (kind == "btrack-accepted")
            {
                expected = vector.GetProperty("expected");
            }
            else
            {
                continue;
            }

            var id = vector.GetProperty("id").GetString()!;
            var projection = File.ReadAllBytes(Path.Combine(SuiteRoot, expected.GetProperty("projection").GetProperty("path").GetString()!));
            var digest = File.ReadAllBytes(Path.Combine(SuiteRoot, expected.GetProperty("rawDigest").GetProperty("path").GetString()!));
            var trackHash = expected.GetProperty("trackHash").GetString()!;
            var yargDigest = Blake3.Hash(projection);
            var yargHash = BTrackHashResult.Encode(yargDigest);
            checkedCount++;

            if (!yargDigest.AsSpan().SequenceEqual(digest) || yargHash != trackHash)
            {
                mismatches.Add($"{id}: digestMatch={yargDigest.AsSpan().SequenceEqual(digest)} hash yarg={yargHash} scan={trackHash}");
            }
        }

        Assert.That(checkedCount, Is.EqualTo(14), "expected 10 source-accepted + 4 btrack-accepted canonical projections");
        Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
    }

    private static void ApplyModifiers(ref ParseSettings settings, JsonElement modifiers)
    {
        var hopoFrequency = modifiers.GetProperty("hopo_frequency").GetInt32();
        if (hopoFrequency != 0)
        {
            settings.HopoThreshold = hopoFrequency;
        }

        var sustainCutoff = modifiers.GetProperty("sustain_cutoff_threshold").GetInt32();
        if (sustainCutoff != ParseSettings.SETTING_DEFAULT)
        {
            settings.SustainCutoffThreshold = sustainCutoff;
        }

        settings.NoteSnapThreshold = modifiers.GetProperty("chord_snap_threshold").GetInt32();

        var multiplierNote = modifiers.GetProperty("multiplier_note").GetInt32();
        if (multiplierNote != 0)
        {
            settings.StarPowerNote = multiplierNote;
        }

        if (modifiers.GetProperty("five_lane_drums").GetBoolean())
        {
            settings.DrumsType = DrumsType.FiveLane;
        }
        else if (modifiers.GetProperty("pro_drums").GetBoolean())
        {
            settings.DrumsType = DrumsType.ProDrums;
        }
    }

    private static Instrument MapInstrument(string name)
    {
        return name switch
        {
            "guitar" => Instrument.FiveFretGuitar,
            "bass" => Instrument.FiveFretBass,
            "rhythm" => Instrument.FiveFretRhythm,
            "guitarcoop" => Instrument.FiveFretCoopGuitar,
            "keys" => Instrument.Keys,
            "drums" => Instrument.FourLaneDrums,
            _ => throw new ArgumentException($"Unknown golden-vector instrument '{name}'."),
        };
    }

    private static Difficulty MapDifficulty(string name)
    {
        return name switch
        {
            "easy" => Difficulty.Easy,
            "medium" => Difficulty.Medium,
            "hard" => Difficulty.Hard,
            "expert" => Difficulty.Expert,
            _ => throw new ArgumentException($"Unknown golden-vector difficulty '{name}'."),
        };
    }

    private static string FirstDiff(byte[] left, byte[] right)
    {
        var n = Math.Min(left.Length, right.Length);
        for (var i = 0; i < n; i++)
        {
            if (left[i] != right[i])
            {
                return $"offset {i} yarg=0x{left[i]:x2} scan=0x{right[i]:x2}";
            }
        }

        return left.Length == right.Length ? "none" : "shared-prefix-then-length";
    }

    private static string? FindScanChartRoot()
    {
        var env = Environment.GetEnvironmentVariable("SCAN_CHART_ROOT");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
        {
            return env;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "scan-chart");
            if (File.Exists(Path.Combine(candidate, "test", "golden-vectors", "suites", "v2", "manifest.json")))
            {
                return candidate;
            }
        }

        return null;
    }
}