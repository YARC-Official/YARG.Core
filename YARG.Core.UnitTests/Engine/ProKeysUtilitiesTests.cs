using NUnit.Framework;
using YARG.Core.Engine.Keys;

namespace YARG.Core.UnitTests.Engine;

public class ProKeysUtilitiesTests
{
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
