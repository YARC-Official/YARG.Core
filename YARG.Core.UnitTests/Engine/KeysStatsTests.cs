using NUnit.Framework;
using YARG.Core.Engine.Keys;

namespace YARG.Core.UnitTests.Engine;

public class KeysStatsTests
{
    [Test]
    public void CopyConstructor_CopiesBaseScoreAndProgressFields()
    {
        var original = new KeysStats
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

        var copy = new KeysStats(original);

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
    public void CopyConstructor_CopiesKeysSpecificFields()
    {
        var original = new KeysStats
        {
            Overhits = 2,
            FatFingersIgnored = 3,
        };

        var copy = new KeysStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Overhits, Is.EqualTo(original.Overhits));
            Assert.That(copy.FatFingersIgnored, Is.EqualTo(original.FatFingersIgnored));
        }
    }

    [Test]
    public void IsFullCombo_UsesTotalChords()
    {
        var stats = new KeysStats
        {
            MaxCombo = 25,
            TotalNotes = 40,
            TotalChords = 25,
        };

        Assert.That(stats.IsFullCombo, Is.True);
    }

    [Test]
    public void Reset_ClearsRunStateButPreservesChartTotals()
    {
        var stats = new KeysStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            NotesHit = 40,
            TotalNotes = 50,
            TotalChords = 45,
            TotalStarPowerPhrases = 6,
            MaxSoloBonusPoints = 300,
            Overhits = 2,
            FatFingersIgnored = 3,
        };

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
            Assert.That(stats.FatFingersIgnored, Is.Zero);
        }
    }
}
