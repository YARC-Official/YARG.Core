using NUnit.Framework;
using Cysharp.Text;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Core.UnitTests.Engine;

public class ScoreThresholdTests : EngineTester
{
    [Test]
    public void CalculateStarScoreThresholds_CombinesBaseAndSoloScoresThenFloors()
    {
        int[] thresholds = YargFiveFretGuitarEngine.CalculateStarScoreThresholds(
            multiplierThresholds: [0.5f, 1.25f],
            soloBonusMultiplierThresholds: [0.1f, 0.3f],
            baseScore: 101,
            soloScore: 33);

        Assert.That(thresholds, Is.EqualTo([53, 136]));
    }

    [Test]
    public void CalculateStarScoreThresholds_AppliesSoloScoreMultiplier()
    {
        int[] thresholds = YargFiveFretGuitarEngine.CalculateStarScoreThresholds(
            multiplierThresholds: [0.5f, 1.25f],
            soloBonusMultiplierThresholds: [0.1f, 0.3f],
            baseScore: 101,
            soloScore: 33,
            scoreMultiplier: 0.5);

        Assert.That(thresholds, Is.EqualTo([52, 131]));
    }

    [Test]
    public void NewEngineHasDefaultStarThresholdsBeforeRegistration()
    {
        var chart = GetChart();
        var difficulty = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
        var parameters = EnginePreset.Default.FiveFretGuitar.Create(
            StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, isBass: false);
        var engine = new YargFiveFretGuitarEngine(difficulty, chart.SyncTrack, parameters, isBot: true);

        Assert.That(engine.StarScoreThresholds, Is.EqualTo(ExpectedThresholds(engine, 1.0)));
    }

    [Test]
    public void GetStarScoreCutoffs_ForBand_SinglePlayerUsesPlayerCutoffs()
    {
        var chart = GetChart();
        var manager = new EngineManager();
        var player = RegisterGuitar(manager, chart, chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert));
        var playerCutoffs = player.BaseEngine.StarScoreThresholds!;

        int[] bandCutoffs = EngineManager.GetStarScoreCutoffs([player]);

        Assert.That(bandCutoffs, Is.EqualTo(playerCutoffs));
    }

    [Test]
    public void GetStarScoreCutoffs_ForBand_MultiplePlayersAppliesBandScaling()
    {
        var chart = GetChart();
        var manager = new EngineManager();
        var difficulty = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
        var firstPlayer = RegisterGuitar(manager, chart, difficulty);
        var secondPlayer = RegisterGuitar(manager, chart, difficulty.Clone());

        var firstPlayerCutoffs = firstPlayer.BaseEngine.StarScoreThresholds!;
        var secondPlayerCutoffs = secondPlayer.BaseEngine.StarScoreThresholds!;
        int[] bandCutoffs = EngineManager.GetStarScoreCutoffs([firstPlayer, secondPlayer]);

        var expectedCutoffs = firstPlayerCutoffs
            .Zip(secondPlayerCutoffs, (first, second) => (int) Math.Floor((first + second) * 1.265))
            .ToArray();

        Assert.That(bandCutoffs, Is.EqualTo(expectedCutoffs));
    }

    [Test]
    public void EngineContainers_AdjustThresholdsForUnisonStarPowerAndNoStarPower()
    {
        var chart = GetChart();
        var manager = new EngineManager();
        var sourceDifficulty = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
        var unisonPlayer = RegisterGuitar(manager, chart, sourceDifficulty);
        var starPowerOnlyPlayer = RegisterGuitar(manager, chart, WithoutStarPowerPhrases(sourceDifficulty));
        var noStarPowerPlayer = RegisterGuitar(manager, chart, WithoutStarPower(WithoutStarPowerPhrases(sourceDifficulty)));

        Assert.Multiple(() =>
        {
            Assert.That(unisonPlayer.StarPowerPresenceStatus,
                Is.EqualTo(EngineManager.EngineContainer.ChartStarPowerPresence.HasUnisons));
            Assert.That(starPowerOnlyPlayer.StarPowerPresenceStatus,
                Is.EqualTo(EngineManager.EngineContainer.ChartStarPowerPresence.HasStarPower));
            Assert.That(noStarPowerPlayer.StarPowerPresenceStatus,
                Is.EqualTo(EngineManager.EngineContainer.ChartStarPowerPresence.None));

            AssertPlayerThresholdMultiplier(unisonPlayer, 1.0);
            AssertPlayerThresholdMultiplier(starPowerOnlyPlayer, 1.2 / 1.3);
            AssertPlayerThresholdMultiplier(noStarPowerPlayer, 1.0 / 1.3);
        });
    }

    [Test]
    public void GetStarScoreCutoffs_ForBand_WeightsStarPowerPresencePerPlayer()
    {
        var chart = GetChart();
        var manager = new EngineManager();
        var sourceDifficulty = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
        var unisonPlayer = RegisterGuitar(manager, chart, sourceDifficulty);
        var starPowerOnlyPlayer = RegisterGuitar(manager, chart, WithoutStarPowerPhrases(sourceDifficulty));
        var noStarPowerPlayer = RegisterGuitar(manager, chart, WithoutStarPower(WithoutStarPowerPhrases(sourceDifficulty)));

        int[] bandCutoffs = EngineManager.GetStarScoreCutoffs([unisonPlayer, starPowerOnlyPlayer, noStarPowerPlayer]);

        var totalStarPowerMultiplier = 0.265 + (0.265 / 2);
        var bandMultiplier = 1.0 + totalStarPowerMultiplier * (3.0 - 1.0) / 3.0;
        var expectedCutoffs = new int[unisonPlayer.BaseEngine.StarScoreThresholds!.Length];
        for (int i = 0; i < expectedCutoffs.Length; i++)
        {
            var totalCutoff = unisonPlayer.BaseEngine.StarScoreThresholds[i]
                + starPowerOnlyPlayer.BaseEngine.StarScoreThresholds![i]
                + noStarPowerPlayer.BaseEngine.StarScoreThresholds![i];
            expectedCutoffs[i] = (int) Math.Floor(totalCutoff * bandMultiplier);
        }

        Assert.That(bandCutoffs, Is.EqualTo(expectedCutoffs));
    }

    [Test]
    public void UpdateStars_ReportsProgressTowardNextThreshold()
    {
        var manager = new EngineManager
        {
            StarScoreThresholds = [100, 200, 300, 400, 500, 600],
            Score = 150,
        };

        manager.UpdateStars();

        Assert.That(manager.Stars, Is.EqualTo(1.5f).Within(0.0000001));
    }

    [Test]
    public void UpdateStars_AtThresholdReportsCompletedStarWithoutAdvancingIndex()
    {
        var manager = new EngineManager
        {
            StarScoreThresholds = [100, 200, 300, 400, 500, 600],
            Score = 100,
        };

        manager.UpdateStars();

        Assert.That(manager.Stars, Is.EqualTo(1.0f).Within(0.0000001));
    }

    private readonly record struct CapturedLogItem(LogLevel Level, string Message);

    private sealed class CapturingLogListener : BaseYargLogListener
    {
        public List<CapturedLogItem> Items { get; } = new();

        public CapturingLogListener()
            : base(new MessageOnlyYargLogFormatter())
        {
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            Items.Add(new CapturedLogItem(item.Level, output.ToString()));
        }
    }

    private static EngineManager.EngineContainer RegisterGuitar(EngineManager manager, SongChart chart,
        InstrumentDifficulty<GuitarNote> difficulty)
    {
        var engineParameters = EnginePreset.Default.FiveFretGuitar.Create(
            StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, isBass: false);
        var engine = new YargFiveFretGuitarEngine(difficulty, chart.SyncTrack, engineParameters, isBot: true);
        return manager.Register(engine, difficulty, chart, RockMeterPreset.Normal);
    }

    private static InstrumentDifficulty<GuitarNote> WithoutStarPowerPhrases(
        InstrumentDifficulty<GuitarNote> sourceDifficulty)
    {
        var difficulty = sourceDifficulty.Clone();
        difficulty.Phrases.RemoveAll(phrase => phrase.Type == PhraseType.StarPower);
        return difficulty;
    }

    private static InstrumentDifficulty<GuitarNote> WithoutStarPower(
        InstrumentDifficulty<GuitarNote> sourceDifficulty)
    {
        var difficulty = sourceDifficulty.Clone();
        const NoteFlags starPowerFlags = NoteFlags.StarPower | NoteFlags.StarPowerStart | NoteFlags.StarPowerEnd;
        foreach (var note in difficulty.Notes)
        {
            foreach (var noteOrChild in note.AllNotes)
            {
                noteOrChild.ClearFlag(starPowerFlags);
            }
        }

        return difficulty;
    }

    private static void AssertPlayerThresholdMultiplier(EngineManager.EngineContainer player, double scoreMultiplier)
    {
        Assert.That(player.BaseEngine.StarScoreThresholds,
            Is.EqualTo(ExpectedThresholds(player.BaseEngine, scoreMultiplier)));
    }

    private static int[] ExpectedThresholds(BaseEngine engine, double scoreMultiplier)
    {
        return YargFiveFretGuitarEngine.CalculateStarScoreThresholds(
            engine.BaseParameters.StarMultiplierThresholds,
            engine.BaseParameters.SoloBonusStarMultiplierThresholds,
            engine.BaseScore,
            engine.BaseStats.MaxSoloBonusPoints,
            scoreMultiplier);
    }
}
