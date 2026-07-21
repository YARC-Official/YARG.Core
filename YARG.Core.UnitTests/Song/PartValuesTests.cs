using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class PartValuesTests
{
    [Test]
    public void Default_HasNoActiveBitsAndIntensityMinusOne()
    {
        var partValues = PartValues.Default;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partValues.SubTracks, Is.Zero);
            Assert.That(partValues.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(partValues.Intensity, Is.EqualTo(-1));
            Assert.That(partValues.IsActive(), Is.False);
            Assert.That(partValues[0], Is.False);
            Assert.That(partValues[Difficulty.Easy], Is.False);
        }
    }

    [Test]
    public void ActivateSubtrack_SetsRequestedBitsAndMarksPartActive()
    {
        var partValues = PartValues.Default;

        partValues.ActivateSubtrack(0);
        partValues.ActivateSubtrack(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partValues.IsActive(), Is.True);
            Assert.That(partValues[0], Is.True);
            Assert.That(partValues[1], Is.False);
            Assert.That(partValues[2], Is.True);
        }
    }

    [Test]
    public void ActivateDifficulty_SetsRequestedDifficultyFlags()
    {
        var partValues = PartValues.Default;

        partValues.ActivateDifficulty(Difficulty.Easy);
        partValues.ActivateDifficulty(Difficulty.Expert);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partValues.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy | DifficultyMask.Expert));
            Assert.That(partValues[Difficulty.Easy], Is.True);
            Assert.That(partValues[Difficulty.Medium], Is.False);
            Assert.That(partValues[Difficulty.Expert], Is.True);
        }
    }

    [Test]
    public void ActivateDifficulty_AlsoMarksPartActiveBecauseBitsOverlapSubtracks()
    {
        var partValues = PartValues.Default;

        partValues.ActivateDifficulty(Difficulty.Easy);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partValues.IsActive(), Is.True);
            Assert.That(partValues.SubTracks, Is.EqualTo((byte) (DifficultyMask.Beginner | DifficultyMask.Easy)));
            Assert.That(partValues[0], Is.True);
            Assert.That(partValues[1], Is.True);
            Assert.That(partValues[2], Is.False);
        }
    }

    [Test]
    public void SubtrackIndexer_ThrowsWhenIndexIsOutOfRange()
    {
        var partValues = PartValues.Default;

        Assert.That(
            () => _ = partValues[8],
            Throws.InstanceOf<IndexOutOfRangeException>().With.Message.EqualTo("Subtrack out of range"));
    }

    [Test]
    public void DifficultyIndexer_ThrowsWhenDifficultyIsOutOfRange()
    {
        var partValues = PartValues.Default;

        Assert.That(
            () => _ = partValues[(Difficulty) 6],
            Throws.Exception.With.Message.EqualTo("Difficulty out of range"));
    }
}