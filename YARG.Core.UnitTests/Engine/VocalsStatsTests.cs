using NUnit.Framework;
using YARG.Core.Engine.Vocals;

namespace YARG.Core.UnitTests.Engine;

public class VocalsStatsTests
{
    [Test]
    public void CopyConstructor_CopiesBaseScoreAndProgressFields()
    {
        var original = new VocalsStats
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

        var copy = new VocalsStats(original);

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
    public void CopyConstructor_CopiesVocalsSpecificFields()
    {
        var original = new VocalsStats
        {
            TicksHit = 80,
            TicksMissed = 20,
            HasCarryNote = true,
        };

        var copy = new VocalsStats(original);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.TicksHit, Is.EqualTo(original.TicksHit));
            Assert.That(copy.TicksMissed, Is.EqualTo(original.TicksMissed));
            Assert.That(copy.TotalTicks, Is.EqualTo(original.TotalTicks));
            Assert.That(copy.HasCarryNote, Is.EqualTo(original.HasCarryNote));
        }
    }

    [Test]
    public void Percent_UsesVocalTicks()
    {
        var stats = new VocalsStats
        {
            NotesHit = 1,
            TotalNotes = 1,
            TicksHit = 80,
            TicksMissed = 20,
        };

        Assert.That(stats.Percent, Is.EqualTo(0.8f).Within(0.0000001));
    }

    [Test]
    public void Reset_ClearsRunStateButPreservesChartTotals()
    {
        var stats = new VocalsStats
        {
            CommittedScore = 100,
            PendingScore = 25,
            NotesHit = 40,
            TotalNotes = 50,
            TotalChords = 45,
            TotalStarPowerPhrases = 6,
            MaxSoloBonusPoints = 300,
            TicksHit = 80,
            TicksMissed = 20,
            HasCarryNote = true,
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
            Assert.That(stats.TicksHit, Is.Zero);
            Assert.That(stats.TicksMissed, Is.Zero);
            Assert.That(stats.TotalTicks, Is.Zero);
            Assert.That(stats.HasCarryNote, Is.False);
        }
    }
}
