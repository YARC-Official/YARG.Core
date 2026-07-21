using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class HashWrapperTests
{
    [Test]
    public void Hash_ComputesExpectedSha1Bytes()
    {
        byte[] data = "hello world"u8.ToArray();

        var hash = HashWrapper.Hash(data);
        byte[] expected = SHA1.HashData(data);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hash.HashBytes, Is.EqualTo(expected));
            Assert.That(hash.ToString(), Is.EqualTo(Convert.ToHexString(expected)));
        }
    }

    [Test]
    public void FromString_ParsesHexCaseInsensitivelyAndRoundTripsToString()
    {
        const string value = "0123456789abcdeffedcba9876543210abcdef12";

        var hash = HashWrapper.FromString(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hash.HashBytes, Is.EqualTo(Convert.FromHexString(value)));
            Assert.That(hash.ToString(), Is.EqualTo(value.ToUpperInvariant()));
        }
    }

    [Test]
    public void Create_EqualsFromStringAndSharesHashCode()
    {
        byte[] bytes = Convert.FromHexString("00112233445566778899AABBCCDDEEFF00112233");

        var created = HashWrapper.Create(bytes);
        var parsed = HashWrapper.FromString("00112233445566778899AABBCCDDEEFF00112233");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Equals(parsed), Is.True);
            Assert.That(created.CompareTo(parsed), Is.Zero);
            Assert.That(created.GetHashCode(), Is.EqualTo(parsed.GetHashCode()));
        }
    }

    [Test]
    public void CompareTo_OrdersDifferentHashes()
    {
        var lower = HashWrapper.FromString("0000000000000000000000000000000000000001");
        var higher = HashWrapper.FromString("0000000000000000000000000000000000000002");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lower.CompareTo(higher), Is.LessThan(0));
            Assert.That(higher.CompareTo(lower), Is.GreaterThan(0));
        }
    }

    [Test]
    public void SerializeAndDeserialize_RoundTripThroughStream()
    {
        var original = HashWrapper.FromString("89ABCDEF0123456789ABCDEF0123456789ABCDEF");
        using MemoryStream stream = new();

        original.Serialize(stream);
        stream.Position = 0;

        var deserialized = HashWrapper.Deserialize(stream);

        Assert.That(deserialized.Equals(original), Is.True);
    }

    [Test]
    public void Deserialize_ThrowsWhenStreamIsTooShort()
    {
        using MemoryStream stream = new(new byte[HashWrapper.HASH_SIZE_IN_BYTES - 1]);

        Assert.That(
            () => HashWrapper.Deserialize(stream),
            Throws.TypeOf<EndOfStreamException>());
    }

    [Test]
    public void Deserialize_RoundTripsThroughFixedArrayStream()
    {
        byte[] bytes = Convert.FromHexString("FEDCBA98765432100123456789ABCDEFFEDCBA98");
        using var fixedArray = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(fixedArray.Span);
        var stream = new FixedArrayStream(fixedArray);

        var deserialized = HashWrapper.Deserialize(ref stream);

        Assert.That(deserialized.HashBytes, Is.EqualTo(bytes));
    }
}