using NUnit.Framework;
using Cysharp.Text;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Logging;

namespace YARG.Core.UnitTests.Engine;

public class ScoreThresholdTests
{
    [Test]
    public void PopulateStarScoreThresholds_CombinesBaseAndSoloScoresThenFloors()
    {
        int[] thresholds = YargFiveFretGuitarEngine.PopulateStarScoreThresholds(
            multiplierThresholds: [0.5f, 1.25f],
            soloBonusMultiplierThresholds: [0.1f, 0.3f],
            baseScore: 101,
            soloScore: 33);

        Assert.That(thresholds, Is.EqualTo([53, 136]));
    }

    [Test]
    public void GetStarScoreCutoffs_ForBand_SinglePlayerUsesPlayerCutoffs()
    {
        var playerCutoffs = new[] { 100, 200, 300, 400, 500, 600 };

        int[] bandCutoffs = EngineManager.GetStarScoreCutoffs([playerCutoffs]);

        Assert.That(bandCutoffs, Is.EqualTo(playerCutoffs));
    }

    [Test]
    public void GetStarScoreCutoffs_ForBand_MultiplePlayersAppliesBandScaling()
    {
        var firstPlayerCutoffs = new[] { 100, 200, 300, 400, 500, 600 };
        var secondPlayerCutoffs = new[] { 50, 100, 150, 200, 250, 300 };

        int[] bandCutoffs = EngineManager.GetStarScoreCutoffs([firstPlayerCutoffs, secondPlayerCutoffs]);

        Assert.That(bandCutoffs, Is.EqualTo([189, 379, 569, 759, 948, 1138]));
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
}