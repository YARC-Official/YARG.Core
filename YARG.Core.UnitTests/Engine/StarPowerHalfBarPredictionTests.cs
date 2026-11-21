using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Core.UnitTests.Engine;

/// Regression for a replay verification mismatch caused by frame timing.
///
/// GenerateQueuedUpdates predicts when whammy gain will fill half a Star Power bar so it can
/// queue the activation update. That prediction used to be anchored to `previousTick` and the
/// SP amount at the time generation happened to run. At 30 FPS this predicted tick 154904;
/// at 60 FPS, 154903. Activating SP on different ticks rebased sustains on different ticks,
/// which changed the final score by 32 points in "The Covasettes - Duvet Thief".
public class StarPowerHalfBarPredictionTests
{
    [Test]
    public void HalfBarTime_IsIndependentOfWhenQueuedUpdatesWereGenerated()
    {
        var sync = new SyncTrack(480);
        sync.Tempos.Add(new TempoChange(120, 0, 0));

        // Two generation snapshots from the same replay, at different frame rates. Both simulate
        // the same SP amount at the same max whammy tick, but started from different ticks.
        const uint maxWhammyTick = 154906;
        const uint simulatedSpAmount = 7683;
        const uint ticksPerHalfBar = 7680;

        double halfBarTime = BaseEngine.CalculateStarPowerHalfBarTime(
            sync, maxWhammyTick, simulatedSpAmount, ticksPerHalfBar);

        Assert.That(sync.TimeToTick(halfBarTime), Is.EqualTo(154904),
            "the prediction must use the simulated amount at maxWhammyTick, not the generation point");
    }
}
