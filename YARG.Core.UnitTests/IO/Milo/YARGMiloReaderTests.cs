using System.Text;
using System.IO.Compression;
using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO.Milo;

public class YARGMiloReaderTests
{
    private static readonly byte[] Barrier = [0xAD, 0xDE, 0xAD, 0xDE];

    [Test]
    public void GetMiloFile_FindsNestedUtf8NamedFile()
    {
        var expected = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var root = new MiloDirectorySpec
        {
            Name = "root",
            SubDirectories =
            {
                new MiloDirectorySpec
                {
                    Name = "venue",
                    Files =
                    {
                        new MiloFileSpec("entry_0", "søng.anim", expected),
                    },
                },
            },
        };

        using var milo = CreateMiloFile(root);
        using var file = YARGMiloReader.GetMiloFile(milo, "søng.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void GetMiloFile_WhenFinalBlockIsMissingBarrier_StillReturnsEarlierFile()
    {
        var firstFile = new byte[] { 0x01, 0x02, 0x03 };
        var root = new MiloDirectorySpec
        {
            Name = "root",
            OmitFinalBarrierForLastFile = true,
            Files =
            {
                new MiloFileSpec("entry_0", "first.anim", firstFile),
                new MiloFileSpec("entry_1", "second.anim", new byte[] { 0x99, 0x98 }),
            },
        };

        using var milo = CreateMiloFile(root);
        using var file = YARGMiloReader.GetMiloFile(milo, "first.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(firstFile));
    }

    [Test]
    public void GetMiloFile_WhenDirectoryListsFileWithoutParsedBlock_ReturnsEmpty()
    {
        var root = new MiloDirectorySpec
        {
            Name = "root",
            OmitFinalBarrierForLastFile = true,
            Files =
            {
                new MiloFileSpec("entry_0", "first.anim", new byte[] { 0x01 }),
                new MiloFileSpec("entry_1", "missing.anim", new byte[] { 0x02, 0x03 }),
            },
        };

        using var milo = CreateMiloFile(root);
        using var file = YARGMiloReader.GetMiloFile(milo, "missing.anim");

        Assert.That(file.Length, Is.Zero);
    }

    [Test]
    public void GetMiloFile_WhenFileDoesNotExist_ReturnsEmpty()
    {
        var root = new MiloDirectorySpec
        {
            Name = "root",
            Files =
            {
                new MiloFileSpec("entry_0", "song.anim", new byte[] { 0x10, 0x11 }),
            },
        };

        using var milo = CreateMiloFile(root);
        using var file = YARGMiloReader.GetMiloFile(milo, "missing.anim");

        Assert.That(file.Length, Is.Zero);
    }

    [TestCase(MiloContainerType.MiloB)]
    [TestCase(MiloContainerType.MiloC)]
    [TestCase(MiloContainerType.MiloD)]
    public void GetMiloFile_DecompressesSupportedCompressedMiloVariants(MiloContainerType containerType)
    {
        var expected = Enumerable.Range(0, 32).Select(i => (byte) i).ToArray();
        var root = new MiloDirectorySpec
        {
            Name = "root",
            Files =
            {
                new MiloFileSpec("entry_0", "song.anim", expected),
            },
        };

        using var milo = CreateMiloFile(root, containerType);
        using var file = YARGMiloReader.GetMiloFile(milo, "song.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void GetMiloFile_ParsesOptionalDirectoryFields_AndMissingU11Branch()
    {
        var expected = new byte[] { 0x21, 0x22, 0x23 };
        var root = new MiloDirectorySpec
        {
            Name = "root",
            IncludeOptionalFields = true,
            MiloU11FlagByte = 0,
            Files =
            {
                new MiloFileSpec("entry_0", "song.anim", expected),
            },
        };

        using var milo = CreateMiloFile(root);
        using var file = YARGMiloReader.GetMiloFile(milo, "song.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void GetMiloFile_WithInvalidMagic_ThrowsInvalidDataException()
    {
        using var milo = CreateInvalidMagicMiloFile();

        Assert.That(() => YARGMiloReader.GetMiloFile(milo, "song.anim"),
            Throws.TypeOf<InvalidDataException>().With.Message.EqualTo("Not a valid Milo file"));
    }

    [Test]
    public void GetMiloFile_WithTooSmallMiloDBlock_ThrowsInvalidDataException()
    {
        using var milo = CreateTooSmallMiloDBlockFile();

        Assert.That(() => YARGMiloReader.GetMiloFile(milo, "song.anim"),
            Throws.TypeOf<InvalidDataException>().With.Message.EqualTo("Data too small to be a valid MiloD"));
    }

    [Test]
    public void GetMiloFile_ReassemblesPayloadAcrossMultipleBlocksInOrder()
    {
        var expected = Enumerable.Range(0, 64).Select(i => (byte) (255 - i)).ToArray();
        var root = new MiloDirectorySpec
        {
            Name = "root",
            IncludeOptionalFields = true,
            Files =
            {
                new MiloFileSpec("entry_0", "song.anim", expected),
            },
        };

        using var milo = CreateMiloFile(
            root,
            MiloContainerType.MiloA,
            new MiloBlockSpec(false),
            new MiloBlockSpec(false),
            new MiloBlockSpec(false));
        using var file = YARGMiloReader.GetMiloFile(milo, "song.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void GetMiloFile_HandlesMixedCompressedAndRawMiloDBlocks()
    {
        var expected = Enumerable.Range(0, 48).Select(i => (byte) (i * 3)).ToArray();
        var root = new MiloDirectorySpec
        {
            Name = "root",
            Files =
            {
                new MiloFileSpec("entry_0", "song.anim", expected),
            },
        };

        using var milo = CreateMiloFile(
            root,
            MiloContainerType.MiloD,
            new MiloBlockSpec(true),
            new MiloBlockSpec(false));
        using var file = YARGMiloReader.GetMiloFile(milo, "song.anim");

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    private static FixedArray<byte> CreateMiloFile(MiloDirectorySpec root,
        MiloContainerType containerType = MiloContainerType.MiloA,
        params MiloBlockSpec[] blockSpecs)
    {
        using var payload = new MemoryStream();
        WriteDirectory(payload, root);
        byte[] payloadBytes = payload.ToArray();
        var effectiveBlockSpecs = blockSpecs.Length > 0
            ? blockSpecs
            : [new MiloBlockSpec(containerType != MiloContainerType.MiloA)];
        byte[][] payloadChunks = SplitPayload(payloadBytes, effectiveBlockSpecs.Length);
        byte[][] encodedBlocks = new byte[effectiveBlockSpecs.Length][];

        for (int i = 0; i < effectiveBlockSpecs.Length; i++)
        {
            encodedBlocks[i] = EncodeBlock(payloadChunks[i], containerType, effectiveBlockSpecs[i].Compressed);
        }

        uint dataOffset = (uint) (16 + effectiveBlockSpecs.Length * 4);
        uint largestBlock = (uint) payloadChunks.Max(chunk => chunk.Length);

        using var milo = new MemoryStream();
        milo.Write(GetMagicNumber(containerType), Endianness.Little);
        milo.Write(dataOffset, Endianness.Little);
        milo.Write((uint) effectiveBlockSpecs.Length, Endianness.Little);
        milo.Write(largestBlock, Endianness.Little);
        for (int i = 0; i < effectiveBlockSpecs.Length; i++)
        {
            milo.Write(GetStoredBlockSize(encodedBlocks[i], containerType, effectiveBlockSpecs[i].Compressed), Endianness.Little);
        }

        foreach (var block in encodedBlocks)
        {
            milo.Write(block);
        }

        byte[] miloBytes = milo.ToArray();
        return CreateFixedArray(miloBytes);
    }

    private static FixedArray<byte> CreateInvalidMagicMiloFile()
    {
        using var milo = new MemoryStream();
        milo.Write(0x12345678u, Endianness.Little);
        milo.Write(20u, Endianness.Little);
        milo.Write(1u, Endianness.Little);
        milo.Write(0u, Endianness.Little);
        milo.Write(0u, Endianness.Little);
        return CreateFixedArray(milo.ToArray());
    }

    private static FixedArray<byte> CreateTooSmallMiloDBlockFile()
    {
        byte[] blockData = { 0x01, 0x02, 0x03, 0x04 };

        using var milo = new MemoryStream();
        milo.Write(GetMagicNumber(MiloContainerType.MiloD), Endianness.Little);
        milo.Write(20u, Endianness.Little);
        milo.Write(1u, Endianness.Little);
        milo.Write(0u, Endianness.Little);
        milo.Write((uint) blockData.Length, Endianness.Little);
        milo.Write(blockData);
        return CreateFixedArray(milo.ToArray());
    }

    private static void WriteDirectory(Stream stream, MiloDirectorySpec spec)
    {
        stream.Write(28u, Endianness.Big);
        WriteBigEndianString(stream, "Dir");
        WriteBigEndianString(stream, spec.Name);
        stream.Write(0u, Endianness.Big);
        stream.Write(0u, Endianness.Big);

        stream.Write((uint) spec.Files.Count, Endianness.Big);
        foreach (var file in spec.Files)
        {
            WriteBigEndianString(stream, file.Name);
            WriteBigEndianString(stream, file.Value);
        }

        stream.Write(0u, Endianness.Big);

        if (spec.IncludeOptionalFields)
        {
            stream.Write(2u, Endianness.Big);
            WriteBigEndianString(stream, $"{spec.Name}_sub");
            stream.Write(3u, Endianness.Big);
            stream.Write(4u, Endianness.Big);
        }

        stream.Write(7u, Endianness.Big);
        for (int i = 0; i < 7 * 12; i++)
        {
            stream.Write(0f, Endianness.Big);
        }

        stream.Write(0u, Endianness.Big);
        stream.WriteByte(0);
        stream.Write(0u, Endianness.Big);

        stream.Write(0u, Endianness.Big);

        stream.WriteByte(1);
        stream.Write((uint) spec.SubDirectories.Count, Endianness.Big);
        foreach (var child in spec.SubDirectories)
        {
            WriteBigEndianString(stream, child.Name);
        }

        stream.WriteByte(spec.MiloU11FlagByte);
        if (spec.MiloU11FlagByte == 1)
        {
            stream.WriteByte(0);
        }

        foreach (var child in spec.SubDirectories)
        {
            WriteDirectory(stream, child);
        }

        stream.WriteByte(0x42);
        stream.Write(Barrier);

        for (int i = 0; i < spec.Files.Count; i++)
        {
            var file = spec.Files[i];
            stream.Write(file.Data);

            bool isLastFile = i == spec.Files.Count - 1;
            if (!(isLastFile && spec.OmitFinalBarrierForLastFile))
            {
                stream.Write(Barrier);
            }
        }
    }

    private static void WriteBigEndianString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.Write((uint) bytes.Length, Endianness.Big);
        stream.Write(bytes);
    }

    private static uint GetMagicNumber(MiloContainerType containerType)
    {
        return containerType switch
        {
            MiloContainerType.MiloA => 0xCABEDEAFu,
            MiloContainerType.MiloB => 0xCBBEDEAFu,
            MiloContainerType.MiloC => 0xCCBEDEAFu,
            MiloContainerType.MiloD => 0xCDBEDEAFu,
            _ => throw new ArgumentOutOfRangeException(nameof(containerType), containerType, null)
        };
    }

    private static uint GetStoredBlockSize(byte[] blockData, MiloContainerType containerType, bool compressed)
    {
        uint size = (uint) blockData.Length;
        if (containerType == MiloContainerType.MiloD)
        {
            return compressed ? size : size | (1u << 24);
        }

        return size;
    }

    private static byte[] EncodeBlock(byte[] payloadBytes, MiloContainerType containerType, bool compressed)
    {
        return containerType switch
        {
            MiloContainerType.MiloA => payloadBytes,
            MiloContainerType.MiloB => CompressDeflate(payloadBytes),
            MiloContainerType.MiloC => CompressGzip(payloadBytes),
            MiloContainerType.MiloD => compressed ? EncodeMiloDBlock(payloadBytes) : payloadBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(containerType), containerType, null)
        };
    }

    private static byte[][] SplitPayload(byte[] payloadBytes, int blockCount)
    {
        var chunks = new byte[blockCount][];
        int offset = 0;

        for (int i = 0; i < blockCount; i++)
        {
            int remainingBytes = payloadBytes.Length - offset;
            int remainingBlocks = blockCount - i;
            int chunkLength = remainingBytes / remainingBlocks;
            if (remainingBytes % remainingBlocks != 0)
            {
                chunkLength++;
            }

            chunks[i] = payloadBytes.AsSpan(offset, chunkLength).ToArray();
            offset += chunkLength;
        }

        return chunks;
    }

    private static byte[] CompressDeflate(byte[] payloadBytes)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(payloadBytes);
        }
        return output.ToArray();
    }

    private static byte[] CompressGzip(byte[] payloadBytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(payloadBytes);
        }
        return output.ToArray();
    }

    private static byte[] EncodeMiloDBlock(byte[] payloadBytes)
    {
        byte[] deflated = CompressDeflate(payloadBytes);
        var block = new byte[deflated.Length + 5];
        block[0] = 0x00;
        block[1] = 0x00;
        block[2] = 0x00;
        block[3] = 0x00;
        deflated.CopyTo(block.AsSpan(4));
        block[^1] = 0x00;
        return block;
    }

    private static FixedArray<byte> CreateFixedArray(byte[] bytes)
    {
        var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);
        return buffer;
    }

    private sealed class MiloDirectorySpec
    {
        public string Name { get; init; } = "dir";

        public bool IncludeOptionalFields { get; init; }

        public bool OmitFinalBarrierForLastFile { get; init; }

        public byte MiloU11FlagByte { get; init; } = 1;

        public List<MiloFileSpec> Files { get; } = new();

        public List<MiloDirectorySpec> SubDirectories { get; } = new();
    }

    public enum MiloContainerType
    {
        MiloA,
        MiloB,
        MiloC,
        MiloD,
    }

    private readonly record struct MiloBlockSpec(bool Compressed);

    private sealed record MiloFileSpec(string Name, string Value, byte[] Data);
}
