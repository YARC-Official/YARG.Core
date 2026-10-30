using System;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Engine.Vocals;

namespace YARG.Core.UnitTests.Engine;

[TestFixture]
public sealed class PartyVocalsDisplayPartResolverTests
{
    // Use distinct matched parts where possible, allow unavoidable duplicates, and leave nonmatching mics unassigned.
    [TestCase(new uint[] { 3, 1 }, new[] { 1, 0 })]
    [TestCase(new uint[] { 6, 6 }, new[] { 1, 2 })]
    [TestCase(new uint[] { 1, 1 }, new[] { 0, 0 })]
    [TestCase(new uint[] { 0, 7, 7, 7 }, new[] { -1, 0, 1, 2 })]
    [TestCase(new uint[] { 7, 7, 7, 7, 7, 7, 7 }, new[] { 0, 1, 2, 0, 0, 0, 0 })]
    [TestCase(new uint[] { 0x80000000, 0x80000001 }, new[] { 31, 0 })]
    public void ResolvesDistinctRepresentativesBeforeValidDuplicates(uint[] masks, int[] expected)
    {
        var actual = new int[masks.Length];
        PartyVocalsDisplayPartResolver.Resolve(masks, actual);
        Assert.That(actual, Is.EqualTo(expected));
    }

    // Checks all 4,096 combinations of four mics matching three parts against a brute-force optimum.
    // Assignments must be valid and repeatable, without changing the input masks.
    [Test]
    public void ExhaustiveSmallMasksAreValidDeterministicAndMaximallyDistinct()
    {
        var masks = new uint[4];
        var actual = new int[4];
        var repeated = new int[4];
        for (int encoded = 0; encoded < 4096; encoded++)
        {
            for (int mic = 0; mic < masks.Length; mic++)
                masks[mic] = (uint) ((encoded >> (3 * mic)) & 7);
            var original = (uint[]) masks.Clone();
            PartyVocalsDisplayPartResolver.Resolve(masks, actual);
            PartyVocalsDisplayPartResolver.Resolve(masks, repeated);
            Assert.That(masks, Is.EqualTo(original));
            Assert.That(repeated, Is.EqualTo(actual));
            for (int mic = 0; mic < masks.Length; mic++)
            {
                if (masks[mic] == 0) Assert.That(actual[mic], Is.EqualTo(-1));
                else Assert.That(actual[mic] >= 0 && (masks[mic] & (1u << actual[mic])) != 0, Is.True);
            }
            int distinct = actual.Where(part => part >= 0).Distinct().Count();
            Assert.That(distinct, Is.EqualTo(BruteForceMaximum(masks, 0, 0)), $"masks {string.Join(',', masks)}");
        }
    }

    // Reusing the output array must not retain old assignments after silence or changed matches.
    [Test]
    public void ReusingDestinationDoesNotRetainAssignmentsAfterSilenceOrPartChanges()
    {
        var output = new int[2];
        PartyVocalsDisplayPartResolver.Resolve(new uint[] { 6, 6 }, output);
        PartyVocalsDisplayPartResolver.Resolve(new uint[] { 0, 0 }, output);
        Assert.That(output, Is.EqualTo(new[] { -1, -1 }));
        PartyVocalsDisplayPartResolver.Resolve(new uint[] { 1, 4 }, output);
        Assert.That(output, Is.EqualTo(new[] { 0, 2 }));
    }

    // An empty candidate list produces no assignments; a destination too small
    // to hold one assignment per microphone is rejected.
    [Test]
    public void EmptyInputsAreValidAndShortDestinationIsRejected()
    {
        PartyVocalsDisplayPartResolver.Resolve(Array.Empty<uint>(), Array.Empty<int>());
        Assert.Throws<ArgumentException>(() => PartyVocalsDisplayPartResolver.Resolve(new uint[2], new int[1]));
    }

    private static int BruteForceMaximum(uint[] masks, int mic, uint represented)
    {
        if (mic == masks.Length)
        {
            int count = 0;
            for (int part = 0; part < 3; part++)
                if ((represented & (1u << part)) != 0) count++;
            return count;
        }
        if (masks[mic] == 0) return BruteForceMaximum(masks, mic + 1, represented);
        int maximum = 0;
        for (int part = 0; part < 3; part++)
            if ((masks[mic] & (1u << part)) != 0)
                maximum = Math.Max(maximum, BruteForceMaximum(masks, mic + 1, represented | (1u << part)));
        return maximum;
    }
}
