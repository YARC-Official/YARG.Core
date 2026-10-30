using System;
using System.Linq;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Chart.Hashing;

namespace YARG.Core.UnitTests.Chart;

public class Blake3Tests
{
    [TestCase(0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262")]
    [TestCase(64, "4eed7141ea4a5cd4b788606bd23f46e212af9cacebacdc7d1f4c6dc7f2511b98")]
    [TestCase(1024, "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af7")]
    [TestCase(1025, "d00278ae47eb27b34faecf67b4fe263f82d5412916c1ffd97c8cb7fb814b8444")]
    [TestCase(4096, "015094013f57a5277b59d8475c0501042c0b642e531b0a1c8f58d2163229e969")]
    public void Hash_KnownLengthVectors_MatchesReference(int length, string expected)
    {
        var data = Enumerable.Range(0, length)
            .Select(i => (byte) (i % 251))
            .ToArray();

        Assert.That(ToHex(Blake3.Hash(data)), Is.EqualTo(expected));
    }

    [Test]
    public void Hash_Abc_MatchesReference()
    {
        Assert.That(ToHex(Blake3.Hash(new byte[] { (byte) 'a', (byte) 'b', (byte) 'c' })),
            Is.EqualTo("6437b3ac38465133ffb63b75273a8db548c558465d79db03fd359c6cd5bd9d85"));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
