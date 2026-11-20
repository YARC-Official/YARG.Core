using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.UnitTests.IO;

namespace YARG.Core.UnitTests.Extensions;

// The test project targets a .NET with Stream.ReadExactly, whose instance method would win over the extension,
// so the helpers under test are called through StreamExtensions explicitly.
public class StreamExtensionsTests
{
    private static readonly byte[] Data = Enumerable.Range(0, 37).Select(i => (byte) (i * 7)).ToArray();

    private static ShortReadStream CreateStream(int maxPerRead) => new(new MemoryStream(Data, writable: false), maxPerRead);

    [Test]
    public void ShortReadStream_ReturnsFewerBytesThanRequested()
    {
        using var stream = CreateStream(3);
        Assert.That(stream.Read(new byte[10], 0, 10), Is.EqualTo(3));
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadFully_Span_FillsBufferAcrossShortReads(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        var buffer = new byte[20];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StreamExtensions.ReadFully(stream, buffer.AsSpan()), Is.EqualTo(buffer.Length));
            Assert.That(buffer, Is.EqualTo(Data[..20]));
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadFully_Array_FillsRangeAcrossShortReads(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        var buffer = new byte[24];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StreamExtensions.ReadFully(stream, buffer, 2, 20), Is.EqualTo(20));
            Assert.That(buffer[2..22], Is.EqualTo(Data[..20]));
            Assert.That(buffer[..2], Is.All.Zero);
            Assert.That(buffer[22..], Is.All.Zero);
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadFully_StopsAtEndOfStream(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        stream.Position = Data.Length - 5;
        var buffer = new byte[16];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StreamExtensions.ReadFully(stream, buffer.AsSpan()), Is.EqualTo(5));
            Assert.That(buffer[..5], Is.EqualTo(Data[^5..]));
            Assert.That(StreamExtensions.ReadFully(stream, buffer, 0, buffer.Length), Is.Zero);
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadExactly_FillsBufferAcrossShortReads(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        var span = new byte[10];
        var array = new byte[10];

        StreamExtensions.ReadExactly(stream, span.AsSpan());
        StreamExtensions.ReadExactly(stream, array, 0, array.Length);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span, Is.EqualTo(Data[..10]));
            Assert.That(array, Is.EqualTo(Data[10..20]));
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadExactly_ThrowsAtEndOfStream(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        stream.Position = Data.Length - 5;

        Assert.Throws<EndOfStreamException>(() => StreamExtensions.ReadExactly(stream, new byte[6].AsSpan()));
        stream.Position = Data.Length - 5;
        Assert.Throws<EndOfStreamException>(() => StreamExtensions.ReadExactly(stream, new byte[6], 0, 6));
    }

    [TestCase(1)]
    [TestCase(3)]
    public void TypedReads_SucceedAcrossShortReads(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);

        ulong value = stream.Read<ulong>(Endianness.Little);
        byte[] bytes = stream.ReadBytes(7);
        var guid = stream.ReadGuid();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(value, Is.EqualTo(BitConverter.ToUInt64(Data, 0)));
            Assert.That(bytes, Is.EqualTo(Data[8..15]));
            Assert.That(guid, Is.EqualTo(new Guid(Data.AsSpan(15, 16))));
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void TypedReads_ThrowAtEndOfStream(int maxPerRead)
    {
        using var stream = CreateStream(maxPerRead);
        stream.Position = Data.Length - 3;

        Assert.Throws<EndOfStreamException>(() => stream.Read<uint>(Endianness.Little));
        stream.Position = Data.Length - 3;
        Assert.Throws<EndOfStreamException>(() => stream.ReadBytes(4));
    }
}
