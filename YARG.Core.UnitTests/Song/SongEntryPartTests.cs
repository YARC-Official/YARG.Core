using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongEntryPartTests
{
    [Test]
    public void VocalsCount_ReturnsZeroWhenNoVocalPartsAreActive()
    {
        var entry = new TestSongEntry();
        Assert.That(entry.VocalsCount, Is.Zero);
    }

    [Test]
    public void VocalsCount_ReturnsOneWhenLeadVocalsArePresent()
    {
        var parts = AvailableParts.Default;
        parts.LeadVocals.ActivateSubtrack(0);

        var entry = CreateEntry(parts);

        Assert.That(entry.VocalsCount, Is.EqualTo(1));
    }

    [Test]
    public void VocalsCount_ReturnsTwoWhenSecondHarmonyPartIsPresent()
    {
        var parts = AvailableParts.Default;
        parts.HarmonyVocals.ActivateSubtrack(1);

        var entry = CreateEntry(parts);

        Assert.That(entry.VocalsCount, Is.EqualTo(2));
    }

    [Test]
    public void VocalsCount_ReturnsThreeWhenThirdHarmonyPartIsPresent()
    {
        var parts = AvailableParts.Default;
        parts.HarmonyVocals.ActivateSubtrack(2);

        var entry = CreateEntry(parts);

        Assert.That(entry.VocalsCount, Is.EqualTo(3));
    }

    [Test]
    public void HasInstrument_ReturnsTrueOnlyForActiveInstrument()
    {
        var parts = AvailableParts.Default;
        parts.ProKeys.ActivateDifficulty(Difficulty.Expert);

        var entry = CreateEntry(parts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.HasInstrument(Instrument.ProKeys), Is.True);
            Assert.That(entry.HasInstrument(Instrument.Keys), Is.False);
            Assert.That(entry.HasInstrument(Instrument.Vocals), Is.False);
        }
    }

    [Test]
    public void HasDifficultyForInstrument_ReturnsBeginnerWhenEasyIsActivated()
    {
        var parts = AvailableParts.Default;
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Easy);

        var entry = CreateEntry(parts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveFretGuitar, DifficultyMask.Beginner), Is.True);
            Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveFretGuitar, DifficultyMask.Easy), Is.True);
            Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveFretGuitar, DifficultyMask.Expert), Is.False);
        }
    }

    [Test]
    public void HasDifficultyForInstrument_DoesNotReturnBeginnerWhenEasyIsNotActivated()
    {
        var parts = AvailableParts.Default;
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Medium);
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Hard);
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Expert);

        var entry = CreateEntry(parts);

        Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveFretGuitar, DifficultyMask.Beginner), Is.False);
    }

    [Test]
    public void HasDifficultyForInstrument_ReturnsTrueWhenRequestedMaskIsSubsetOfAvailableDifficulties()
    {
        var parts = AvailableParts.Default;
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Easy);
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Medium);
        parts.FiveFretGuitar.ActivateDifficulty(Difficulty.Expert);

        var entry = CreateEntry(parts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                entry.HasDifficultyForInstrument(
                    Instrument.FiveFretGuitar,
                    DifficultyMask.Easy | DifficultyMask.Medium),
                Is.True);
            Assert.That(
                entry.HasDifficultyForInstrument(
                    Instrument.FiveFretGuitar,
                    DifficultyMask.Hard | DifficultyMask.Expert),
                Is.False);
        }
    }

    [Test]
    public void HasDifficultyForInstrument_SingleDifficultyOverloadUsesSameLookup()
    {
        var parts = AvailableParts.Default;
        parts.FiveLaneDrums.ActivateDifficulty(Difficulty.Hard);

        var entry = CreateEntry(parts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveLaneDrums, Difficulty.Hard), Is.True);
            Assert.That(entry.HasDifficultyForInstrument(Instrument.FiveLaneDrums, Difficulty.Expert), Is.False);
        }
    }

    [Test]
    public void HasEmhxDifficultiesForInstrument_RequiresEasyMediumHardAndExpert()
    {
        var completeParts = AvailableParts.Default;
        completeParts.Keys.ActivateDifficulty(Difficulty.Easy);
        completeParts.Keys.ActivateDifficulty(Difficulty.Medium);
        completeParts.Keys.ActivateDifficulty(Difficulty.Hard);
        completeParts.Keys.ActivateDifficulty(Difficulty.Expert);

        var incompleteParts = AvailableParts.Default;
        incompleteParts.Keys.ActivateDifficulty(Difficulty.Easy);
        incompleteParts.Keys.ActivateDifficulty(Difficulty.Medium);
        incompleteParts.Keys.ActivateDifficulty(Difficulty.Hard);

        var completeEntry = CreateEntry(completeParts);
        var incompleteEntry = CreateEntry(incompleteParts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(completeEntry.HasEmhxDifficultiesForInstrument(Instrument.Keys), Is.True);
            Assert.That(incompleteEntry.HasEmhxDifficultiesForInstrument(Instrument.Keys), Is.False);
        }
    }

    [Test]
    public void HasDifficultyForInstrument_ThrowsForUnhandledInstrument()
    {
        var entry = new TestSongEntry();

        Assert.That(
            () => entry.HasDifficultyForInstrument((Instrument) 99, Difficulty.Expert),
            Throws.ArgumentException.With.Message.EqualTo("Unhandled instrument (Parameter 'instrument')"));
    }

    private static TestSongEntry CreateEntry(AvailableParts parts)
    {
        var entry = new TestSongEntry();
        entry.SetParts(parts);
        return entry;
    }
}
