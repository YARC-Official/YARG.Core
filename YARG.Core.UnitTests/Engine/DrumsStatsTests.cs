using NUnit.Framework;
using YARG.Core.Engine.Drums;

namespace YARG.Core.UnitTests.Engine;

public class DrumsStatsTests
{
    [Test]
    public void CopyConstructor_CopiesBaseScoreAndProgressFields()
    {
        var original = new DrumsStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            NotesHit = 40,
            LanedNotesHit = 7,
            TotalNotes = 50,
            TotalChords = 45,
            SoloBonuses = 200,
            MaxSoloBonusPoints = 300,
            CodaBonuses = 400,
            Stars = 4.25f,
        };

        var copy = new DrumsStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.TotalScore, Is.EqualTo(original.TotalScore));
            Assert.That(copy.NotesHit, Is.EqualTo(original.NotesHit));
            Assert.That(copy.LanedNotesHit, Is.EqualTo(original.LanedNotesHit));
            Assert.That(copy.TotalNotes, Is.EqualTo(original.TotalNotes));
            Assert.That(copy.TotalChords, Is.EqualTo(original.TotalChords));
            Assert.That(copy.MaxSoloBonusPoints, Is.EqualTo(original.MaxSoloBonusPoints));
            Assert.That(copy.Stars, Is.EqualTo(original.Stars));
        }
    }

    [Test]
    public void CopyConstructor_CopiesDrumsSpecificFields()
    {
        var original = new DrumsStats
        {
            GhostsHit = 2,
            TotalGhosts = 3,
            AccentsHit = 4,
            TotalAccents = 5,
            DynamicsBonus = 6,
        };
        original.RecordOverhit(10);
        original.RecordOverhit(10);
        original.RecordOverhit(11);
        original.RecordOverhit(null);

        var copy = new DrumsStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Overhits, Is.EqualTo(original.Overhits));
            Assert.That(copy.OverhitsByAction, Is.EqualTo(original.OverhitsByAction));
            Assert.That(copy.GhostsHit, Is.EqualTo(original.GhostsHit));
            Assert.That(copy.TotalGhosts, Is.EqualTo(original.TotalGhosts));
            Assert.That(copy.AccentsHit, Is.EqualTo(original.AccentsHit));
            Assert.That(copy.TotalAccents, Is.EqualTo(original.TotalAccents));
            Assert.That(copy.DynamicsBonus, Is.EqualTo(original.DynamicsBonus));
        }
    }

    [Test]
    public void Reset_ClearsRunStateButPreservesChartTotals()
    {
        var stats = new DrumsStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            NotesHit = 40,
            TotalNotes = 50,
            TotalChords = 45,
            TotalStarPowerPhrases = 6,
            MaxSoloBonusPoints = 300,
            GhostsHit = 2,
            TotalGhosts = 3,
            AccentsHit = 4,
            TotalAccents = 5,
            DynamicsBonus = 6,
        };
        stats.RecordOverhit(10);

        stats.Reset();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.TotalScore, Is.Zero);
            Assert.That(stats.NotesHit, Is.Zero);
            Assert.That(stats.TotalNotes, Is.EqualTo(50));
            Assert.That(stats.TotalChords, Is.EqualTo(45));
            Assert.That(stats.TotalStarPowerPhrases, Is.EqualTo(6));
            Assert.That(stats.MaxSoloBonusPoints, Is.EqualTo(300));
            Assert.That(stats.Overhits, Is.Zero);
            Assert.That(stats.OverhitsByAction, Is.Empty);
            Assert.That(stats.GhostsHit, Is.Zero);
            Assert.That(stats.TotalGhosts, Is.EqualTo(3));
            Assert.That(stats.AccentsHit, Is.Zero);
            Assert.That(stats.TotalAccents, Is.EqualTo(5));
            Assert.That(stats.DynamicsBonus, Is.Zero);
        }
    }
}
