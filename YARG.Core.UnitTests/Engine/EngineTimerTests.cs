using NUnit.Framework;
using YARG.Core.Engine;

namespace YARG.Core.UnitTests.Engine;

public class EngineTimerTests
{
    [Test]
    public void NewTimer_StartsInactiveAndFormatsAsNotStarted()
    {
        var timer = new EngineTimer("Test", 1.5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.SpeedAdjustedThreshold, Is.EqualTo(1.5).Within(0.0000001));
            Assert.That(timer.ToString(), Is.EqualTo("Not started"));
        }
    }

    [Test]
    public void Start_ActivatesTimerAndSetsStartAndEndTimes()
    {
        var timer = new EngineTimer("Test", 2.0);

        timer.Start(5.0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timer.IsActive, Is.True);
            Assert.That(timer.StartTime, Is.EqualTo(5.0).Within(0.0000001));
            Assert.That(timer.EndTime, Is.EqualTo(7.0).Within(0.0000001));
            Assert.That(timer.IsExpired(6.9), Is.False);
            Assert.That(timer.IsExpired(7.0), Is.True);
        }
    }

    [Test]
    public void SetSpeed_ChangesAdjustedThresholdAndEndTime()
    {
        var timer = new EngineTimer("Test", 2.0);

        timer.SetSpeed(1.5);
        timer.Start(3.0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timer.SpeedAdjustedThreshold, Is.EqualTo(3.0).Within(0.0000001));
            Assert.That(timer.EndTime, Is.EqualTo(6.0).Within(0.0000001));
        }
    }

    [Test]
    public void StartWithOffset_BackdatesStartTimeBasedOnThresholdDifference()
    {
        var timer = new EngineTimer("Test", 2.0);

        // `offset` is effectively the amount of time the timer should have remaining,
        // so the timer is started as if it had already been running for
        // `threshold - offset` seconds. This method should likely be renamed for clarity in a future PR.
        timer.StartWithOffset(10.0, 0.5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timer.IsActive, Is.True);
            Assert.That(timer.StartTime, Is.EqualTo(8.5).Within(0.0000001));
            Assert.That(timer.EndTime, Is.EqualTo(10.5).Within(0.0000001));
        }
    }

    [Test]
    public void DisableAndReset_DeactivateTimerAndResetStartTime()
    {
        var timer = new EngineTimer("Test", 2.0);

        timer.Start(4.0);
        timer.Disable(6.0);
        timer.Reset();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.ToString(), Is.EqualTo("Not started"));
        }
    }

    [Test]
    public void StaticHelpers_UpdateReferencedStartTimes()
    {
        double startTime = 0;

        EngineTimer.Start(ref startTime, 12.0);
        Assert.That(startTime, Is.EqualTo(12.0).Within(0.0000001));

        EngineTimer.StartWithOffset(ref startTime, 20.0, threshold: 4.0, offset: 1.5);
        Assert.That(startTime, Is.EqualTo(17.5).Within(0.0000001));

        EngineTimer.Reset(ref startTime);
        Assert.That(startTime, Is.EqualTo(double.MaxValue));
    }

    [Test]
    public void ToString_FormatsStartedTimerWithSixDecimalPlaces()
    {
        var timer = new EngineTimer("Test", 1.25);

        timer.Start(2.5);

        Assert.That(timer.ToString(), Is.EqualTo("2.500000 - 3.750000"));
    }
}
