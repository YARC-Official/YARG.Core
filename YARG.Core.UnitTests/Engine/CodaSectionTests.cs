using NUnit.Framework;
using YARG.Core.Engine;

namespace YARG.Core.UnitTests.Engine;

public class CodaSectionTests
{
    [Test]
    public void Constructor_ConfiguresFretModeScoring()
    {
        var coda = new CodaSection(scoringZones: 5, startTime: 10.0, endTime: 15.0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coda.ScoringZones, Is.EqualTo(5));
            Assert.That(coda.MaxLaneScore, Is.EqualTo(150));
            Assert.That(coda.TotalCodaBonus, Is.Zero);
            Assert.That(coda.StartTime, Is.EqualTo(10.0).Within(0.0000001));
            Assert.That(coda.EndTime, Is.EqualTo(15.0).Within(0.0000001));
            Assert.That(coda.Success, Is.True);
        }
    }

    [Test]
    public void Constructor_ConfiguresSingleZoneDrumScoring()
    {
        var coda = new CodaSection(scoringZones: 1, startTime: 10.0, endTime: 15.0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coda.ScoringZones, Is.EqualTo(1));
            Assert.That(coda.MaxLaneScore, Is.EqualTo(750));
            Assert.That(coda.Success, Is.True);
        }
    }

    [Test]
    public void HitLane_AwardsRechargeScaledBonusForFretLanes()
    {
        var coda = new CodaSection(scoringZones: 5, startTime: 10.0, endTime: 15.0);

        coda.HitLane(time: 10.0, action: 2);
        coda.HitLane(time: 10.75, action: 2);
        coda.HitLane(time: 12.25, action: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coda.TotalCodaBonus, Is.EqualTo(375));
            Assert.That(coda.GetTimeSinceLastHit(fret: 2, time: 12.5), Is.EqualTo(0.25).Within(0.0000001));
        }
    }

    [Test]
    public void HitLane_SingleZoneModeUsesOneSharedRecharge()
    {
        var coda = new CodaSection(scoringZones: 1, startTime: 10.0, endTime: 15.0);

        coda.HitLane(time: 10.0, action: 0);
        coda.HitLane(time: 10.75, action: 3);
        coda.HitLane(time: 12.25, action: 4);

        Assert.That(coda.TotalCodaBonus, Is.EqualTo(1875));
    }

    [Test]
    public void HitLane_RemapsBoundaryLaneToFirstScoringZone()
    {
        var coda = new CodaSection(scoringZones: 5, startTime: 10.0, endTime: 15.0);

        coda.HitLane(time: 10.0, action: 0);
        coda.HitLane(time: 10.75, action: 5);

        Assert.That(coda.TotalCodaBonus, Is.EqualTo(225));
    }

    [Test]
    public void HitLane_UsesConfiguredLaneIndexesForScoringButReportsOriginalLane()
    {
        var coda = new CodaSection(scoringZones: 5, startTime: 10.0, endTime: 15.0);
        int? reportedLane = null;
        coda.OnLaneHit += lane => reportedLane = lane;
        coda.SetLaneIndexes(new Dictionary<int, int>
        {
            [7] = 1,
        });

        coda.HitLane(time: 10.0, action: 1);
        coda.HitLane(time: 10.75, action: 7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coda.TotalCodaBonus, Is.EqualTo(225));
            Assert.That(reportedLane, Is.EqualTo(7));
        }
    }

    [Test]
    public void MissNoteOverhitAndReset_UpdateSuccessAndBonusState()
    {
        var coda = new CodaSection(scoringZones: 5, startTime: 10.0, endTime: 15.0);

        coda.HitLane(time: 10.0, action: 0);
        coda.MissNote();
        coda.Reset(earnedBonus: 123);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coda.Success, Is.True);
            Assert.That(coda.TotalCodaBonus, Is.EqualTo(123));
        }

        coda.Overhit();

        Assert.That(coda.Success, Is.False);
    }

    [TestCase(4.0, 5.0, 0.0f)]
    [TestCase(5.75, 5.0, 0.5f)]
    [TestCase(7.0, 5.0, 1.0f)]
    public void GetNormalizedTimeSinceLastHit_ClampsToRechargeRange(
        double visualTime,
        double mostRecentTime,
        float expected)
    {
        float normalized = CodaSection.GetNormalizedTimeSinceLastHit(visualTime, mostRecentTime);

        Assert.That(normalized, Is.EqualTo(expected).Within(0.0000001));
    }
}