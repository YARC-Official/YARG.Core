using NUnit.Framework;
using YARG.Core.Engine.Keys;

// pattern: Functional Core

namespace YARG.Core.UnitTests.Engine;

public class ProKeysUtilitiesTests
{
    [TestCase(false, 0, 0)]
    [TestCase(true, 0, 1)]
    [TestCase(false, 1, 2)]
    [TestCase(true, 1, 3)]
    [TestCase(false, 2, 4)]
    [TestCase(false, 3, 5)]
    [TestCase(true, 2, 6)]
    [TestCase(false, 4, 7)]
    [TestCase(true, 3, 8)]
    [TestCase(false, 5, 9)]
    [TestCase(true, 4, 10)]
    [TestCase(false, 6, 11)]
    [TestCase(false, 7, 12)]
    [TestCase(true, 5, 13)]
    [TestCase(false, 8, 14)]
    [TestCase(true, 6, 15)]
    [TestCase(false, 9, 16)]
    public void GetKeyIndexForColor_UsesTheLowCWindow(bool black, int colorIndex, int expected)
    {
        Assert.That(ProKeysUtilities.GetKeyIndexForColor(black, colorIndex), Is.EqualTo(expected));
    }

    [TestCase(false, -1)]
    [TestCase(false, ProKeysUtilities.WHITE_KEY_COUNT)]
    [TestCase(true, -1)]
    [TestCase(true, ProKeysUtilities.BLACK_KEY_COUNT)]
    public void GetKeyIndexForColor_RejectsInvalidColorIndex(bool black, int colorIndex)
    {
        Assert.That(() => ProKeysUtilities.GetKeyIndexForColor(black, colorIndex),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [TestCase(ProKeysUtilities.LOW_C, false)]
    [TestCase(ProKeysUtilities.LOW_C_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_D, false)]
    [TestCase(ProKeysUtilities.LOW_D_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_E, false)]
    [TestCase(ProKeysUtilities.LOW_F, false)]
    [TestCase(ProKeysUtilities.LOW_F_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_G, false)]
    [TestCase(ProKeysUtilities.LOW_G_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_A, false)]
    [TestCase(ProKeysUtilities.LOW_A_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_B, false)]
    public void IsBlackKey_IdentifiesBlackKeysWithinOneOctave(int noteIndex, bool expected)
    {
        Assert.That(ProKeysUtilities.IsBlackKey(noteIndex), Is.EqualTo(expected));
    }

    [TestCase(ProKeysUtilities.LOW_C)]
    [TestCase(ProKeysUtilities.LOW_D)]
    [TestCase(ProKeysUtilities.LOW_E)]
    [TestCase(ProKeysUtilities.LOW_F)]
    [TestCase(ProKeysUtilities.LOW_G)]
    [TestCase(ProKeysUtilities.LOW_A)]
    [TestCase(ProKeysUtilities.LOW_B)]
    public void IsWhiteKey_IsInverseOfBlackKeyForWhiteKeys(int noteIndex)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProKeysUtilities.IsWhiteKey(noteIndex), Is.True);
            Assert.That(ProKeysUtilities.IsBlackKey(noteIndex), Is.False);
        }
    }

    [TestCase(ProKeysUtilities.LOW_D_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_A_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_C_SHARP, false)]
    [TestCase(ProKeysUtilities.LOW_F_SHARP, false)]
    public void IsGapOnNextBlackKey_IdentifiesBlackKeyGaps(int noteIndex, bool expected)
    {
        Assert.That(ProKeysUtilities.IsGapOnNextBlackKey(noteIndex), Is.EqualTo(expected));
    }

    [TestCase(ProKeysUtilities.LOW_C, true)]
    [TestCase(ProKeysUtilities.LOW_E, true)]
    [TestCase(ProKeysUtilities.LOW_F, false)]
    [TestCase(ProKeysUtilities.LOW_B, false)]
    public void HalfKeyHelpers_SplitOneOctaveAtEAndF(int noteIndex, bool isLowerHalf)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProKeysUtilities.IsLowerHalfKey(noteIndex), Is.EqualTo(isLowerHalf));
            Assert.That(ProKeysUtilities.IsUpperHalfKey(noteIndex), Is.EqualTo(!isLowerHalf));
        }
    }

    [TestCase(ProKeysUtilities.LOW_E, ProKeysUtilities.LOW_F, true)]
    [TestCase(ProKeysUtilities.LOW_C, ProKeysUtilities.LOW_D, true)]
    [TestCase(ProKeysUtilities.LOW_C_SHARP, ProKeysUtilities.LOW_D_SHARP, true)]
    [TestCase(ProKeysUtilities.LOW_D_SHARP, ProKeysUtilities.LOW_F_SHARP, false)]
    [TestCase(ProKeysUtilities.LOW_C, ProKeysUtilities.LOW_D_SHARP, false)]
    public void IsAdjacentKey_UsesSemitoneAndSameColorNeighbors(
        int noteIndex,
        int adjacentNoteIndex,
        bool expected)
    {
        Assert.That(ProKeysUtilities.IsAdjacentKey(noteIndex, adjacentNoteIndex), Is.EqualTo(expected));
    }
}
