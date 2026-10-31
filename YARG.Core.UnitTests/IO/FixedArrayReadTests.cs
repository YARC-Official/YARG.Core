using NUnit.Framework;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO;

public class FixedArrayReadTests
{
    private static readonly byte[] Data = Enumerable.Range(0, 300).Select(i => (byte) (i % 251)).ToArray();

    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public void Read_FillsBufferAcrossShortReads(int maxPerRead, bool vectorize)
    {
        using var stream = new ShortReadStream(new MemoryStream(Data, writable: false), maxPerRead);
        stream.Position = 10;

        using var array = FixedArray.Read(stream, 250, vectorize);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(array.ReadOnlySpan.ToArray(), Is.EqualTo(Data[10..260]));
            Assert.That(stream.Position, Is.EqualTo(260));
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ReadRemainder_ReadsToEndAcrossShortReads(int maxPerRead)
    {
        using var stream = new ShortReadStream(new MemoryStream(Data, writable: false), maxPerRead);
        stream.Position = 100;

        using var array = FixedArray.ReadRemainder(stream);

        Assert.That(array.ReadOnlySpan.ToArray(), Is.EqualTo(Data[100..]));
    }
}
