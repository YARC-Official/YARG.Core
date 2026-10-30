using System;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongEntryBTrackHashingTests
{
    [Test]
    public void TryGetBTrackHash_LoadsChartOnDemand_AndCachesResult()
    {
        var entry = new TestSongEntry();
        entry.SetChart(CreateGuitarChart());

        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out var result), Is.True);
        Assert.That(result.Hash, Is.EqualTo(ExpectedGuitarHash()));

        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out var cachedResult), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cachedResult.Hash, Is.EqualTo(result.Hash));
            Assert.That(entry.LoadChartCallCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void TryGetBTrackHash_WithProvidedChart_DoesNotLoadSongEntryChart()
    {
        var entry = new TestSongEntry();

        Assert.That(entry.TryGetBTrackHash(CreateGuitarChart(), Instrument.FiveFretGuitar, Difficulty.Expert,
            out var result), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Hash, Is.EqualTo(ExpectedGuitarHash()));
            Assert.That(entry.LoadChartCallCount, Is.Zero);
        }
    }

    [Test]
    public void TryGetBTrackHash_WithProvidedChart_CachesForLaterOnDemandLookup()
    {
        var entry = new TestSongEntry();

        Assert.That(entry.TryGetBTrackHash(CreateGuitarChart(), Instrument.FiveFretGuitar, Difficulty.Expert,
            out var result), Is.True);
        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out var cachedResult), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cachedResult.Hash, Is.EqualTo(result.Hash));
            Assert.That(entry.LoadChartCallCount, Is.Zero);
        }
    }

    [Test]
    public void TryGetBTrackHash_ReturnsFalse_WhenSongEntryChartCannotLoad()
    {
        var entry = new TestSongEntry();

        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out _), Is.False);
        Assert.That(entry.LoadChartCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TryGetBTrackHash_ReturnsFalse_ForUnsupportedInstrument()
    {
        var entry = new TestSongEntry();
        entry.SetChart(CreateGuitarChart());

        Assert.That(entry.TryGetBTrackHash(Instrument.ProGuitar_17Fret, Difficulty.Expert, out _), Is.False);
        Assert.That(entry.LoadChartCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TryGetBTrackHash_Vocals_CachesLeaderboardHash()
    {
        var entry = new TestSongEntry();
        entry.SetChart(CreateVocalsChart());

        Assert.That(entry.TryGetBTrackHash(Instrument.Vocals, Difficulty.Expert, out var result), Is.True);
        Assert.That(entry.GetLeaderboardHash(Instrument.Vocals, Difficulty.Expert), Is.EqualTo(result.Hash));
        Assert.That(entry.LoadChartCallCount, Is.EqualTo(1));
    }

    [Test]
    public void TryGetBTrackHash_DoesNotCacheFailures()
    {
        var entry = new TestSongEntry();
        entry.SetChart(new SongChart(480));

        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out _), Is.False);

        entry.SetChart(CreateGuitarChart());

        Assert.That(entry.TryGetBTrackHash(Instrument.FiveFretGuitar, Difficulty.Expert, out var result), Is.True);
        Assert.That(result.Hash, Is.EqualTo(ExpectedGuitarHash()));
    }

    [Test]
    public void TryGetBTrackHash_ProvidedChartThrows_WhenChartIsNull()
    {
        var entry = new TestSongEntry();

        Assert.That(() => entry.TryGetBTrackHash(null!, Instrument.FiveFretGuitar, Difficulty.Expert, out _),
            Throws.ArgumentNullException.With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("chart"));
    }

    [Test]
    public void GetLeaderboardHash_FallsBackToSongHash_WhenBTrackIsUnavailable()
    {
        var entry = new TestSongEntry();
        var songHash = HashWrapper.FromString("00112233445566778899AABBCCDDEEFF00112233");
        entry.SetHash(songHash);
        entry.SetChart(new SongChart(480));

        var leaderboardHash = entry.GetLeaderboardHash(Instrument.FiveFretGuitar, Difficulty.Expert);

        Assert.That(leaderboardHash, Is.EqualTo(songHash.ToString()));
    }

    private static SongChart CreateGuitarChart()
    {
        var chart = new SongChart(480);
        var difficulty = new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, Difficulty.Expert);
        difficulty.Notes.Add(new GuitarNote(FiveFretGuitarFret.Green, GuitarNoteType.Strum,
            GuitarNoteFlags.None, NoteFlags.None, 0, 1, 0, 1));
        chart.FiveFretGuitar.AddDifficulty(Difficulty.Expert, difficulty);
        return chart;
    }

    private static string ExpectedGuitarHash()
    {
        Assert.That(BTrackHasher.TryCalculateTrackHash(CreateGuitarChart(), Instrument.FiveFretGuitar, Difficulty.Expert, out var result), Is.True);
        return result.Hash;
    }

    private static SongChart CreateVocalsChart()
    {
        var chart = new SongChart(480);
        var parent = new VocalNote(NoteFlags.None, false, 0, 0.1, 0, 120);
        parent.AddChildNote(new VocalNote(60, 0, VocalNoteType.Lyric, 0, 0.1, 0, 60));
        chart.Vocals = new VocalsTrack(Instrument.Vocals, [
            new VocalsPart(false, [new VocalsPhrase(0, 0.1, 0, 120, parent, [])], [], [], [], [])
        ], []);
        return chart;
    }
}
