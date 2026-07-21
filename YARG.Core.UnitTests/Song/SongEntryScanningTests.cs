using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongEntryScanningTests
{
    [Test]
    public void FinalizeDrums_KeepsFourLaneDifficultiesWhenFourLaneFlagIsPresent()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Hard);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.FourLane);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Hard));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
        }
    }

    [Test]
    public void FinalizeDrums_CopiesFourLaneDifficultiesToProDrumsWhenChartIsProOnly()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Expert);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.ProDrums);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
        }
    }

    [Test]
    public void FinalizeDrums_MovesFourLaneDifficultiesToFiveLaneWhenChartIsFiveLaneOnly()
    {
        var parts = AvailableParts.Default;
        parts.FourLaneDrums.ActivateDifficulty(Difficulty.Medium);

        var finalized = TestSongEntry.FinalizeDrumsForTest(parts, DrumsType.FiveLane);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.ProDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Medium));
        }
    }

    [Test]
    public void IsValid_ReturnsFalseForDefaultParts()
    {
        Assert.That(TestSongEntry.IsValidForTest(AvailableParts.Default), Is.False);
    }

    [Test]
    public void IsValid_ReturnsTrueForRepresentativeActiveParts()
    {
        var guitarParts = AvailableParts.Default;
        guitarParts.FiveFretGuitar.ActivateDifficulty(Difficulty.Easy);

        var drumParts = AvailableParts.Default;
        drumParts.ProDrums.ActivateDifficulty(Difficulty.Hard);

        var proKeysParts = AvailableParts.Default;
        proKeysParts.ProKeys.ActivateDifficulty(Difficulty.Expert);

        var vocalParts = AvailableParts.Default;
        vocalParts.HarmonyVocals.ActivateSubtrack(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TestSongEntry.IsValidForTest(guitarParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(drumParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(proKeysParts), Is.True);
            Assert.That(TestSongEntry.IsValidForTest(vocalParts), Is.True);
        }
    }
}
