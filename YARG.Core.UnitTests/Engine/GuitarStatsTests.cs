using NUnit.Framework;
using YARG.Core.Engine.Guitar;

namespace YARG.Core.UnitTests.Engine;

public class GuitarStatsTests
{
    [Test]
    public void CopyConstructor_CopiesBaseScoreAndProgressFields()
    {
        var original = new GuitarStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            NoteScore = 70,
            SustainScore = 20,
            MultiplierScore = 10,
            BandBonusScore = 5,
            Combo = 12,
            MaxCombo = 34,
            ScoreMultiplier = 3,
            BandMultiplier = 2,
            NotesHit = 40,
            LanedNotesHit = 7,
            TotalNotes = 50,
            TotalChords = 45,
            SoloBonuses = 200,
            MaxSoloBonusPoints = 300,
            CodaBonuses = 400,
            StarPowerScore = 500,
            Stars = 4.25f,
        };

        var copy = new GuitarStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.TotalScore, Is.EqualTo(original.TotalScore));
            Assert.That(copy.Combo, Is.EqualTo(original.Combo));
            Assert.That(copy.MaxCombo, Is.EqualTo(original.MaxCombo));
            Assert.That(copy.ScoreMultiplier, Is.EqualTo(original.ScoreMultiplier));
            Assert.That(copy.BandMultiplier, Is.EqualTo(original.BandMultiplier));
            Assert.That(copy.NotesHit, Is.EqualTo(original.NotesHit));
            Assert.That(copy.LanedNotesHit, Is.EqualTo(original.LanedNotesHit));
            Assert.That(copy.TotalNotes, Is.EqualTo(original.TotalNotes));
            Assert.That(copy.TotalChords, Is.EqualTo(original.TotalChords));
            Assert.That(copy.MaxSoloBonusPoints, Is.EqualTo(original.MaxSoloBonusPoints));
            Assert.That(copy.Stars, Is.EqualTo(original.Stars));
        }
    }

    [Test]
    public void CopyConstructor_CopiesGuitarSpecificFields()
    {
        var original = new GuitarStats
        {
            Overstrums = 2,
            HoposStrummed = 3,
            GhostInputs = 4,
        };

        var copy = new GuitarStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Overstrums, Is.EqualTo(original.Overstrums));
            Assert.That(copy.HoposStrummed, Is.EqualTo(original.HoposStrummed));
            Assert.That(copy.GhostInputs, Is.EqualTo(original.GhostInputs));
        }
    }

    [Test]
    public void Reset_ClearsRunStateButPreservesChartTotals()
    {
        var stats = new GuitarStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            Combo = 12,
            MaxCombo = 34,
            ScoreMultiplier = 3,
            BandMultiplier = 2,
            NotesHit = 40,
            TotalNotes = 50,
            TotalChords = 45,
            TotalStarPowerPhrases = 6,
            MaxSoloBonusPoints = 300,
            Overstrums = 2,
            HoposStrummed = 3,
            GhostInputs = 4,
        };

        stats.Reset();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.TotalScore, Is.Zero);
            Assert.That(stats.Combo, Is.Zero);
            Assert.That(stats.MaxCombo, Is.Zero);
            Assert.That(stats.ScoreMultiplier, Is.EqualTo(1));
            Assert.That(stats.BandMultiplier, Is.EqualTo(1));
            Assert.That(stats.NotesHit, Is.Zero);
            Assert.That(stats.TotalNotes, Is.EqualTo(50));
            Assert.That(stats.TotalChords, Is.EqualTo(45));
            Assert.That(stats.TotalStarPowerPhrases, Is.EqualTo(6));
            Assert.That(stats.MaxSoloBonusPoints, Is.EqualTo(300));
            Assert.That(stats.Overstrums, Is.Zero);
            Assert.That(stats.HoposStrummed, Is.Zero);
            Assert.That(stats.GhostInputs, Is.Zero);
        }
    }
}
