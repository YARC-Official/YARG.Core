using System.Text;
using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO.Milo;

public class YARGMiloReaderTests
{
    private static readonly byte[] BARRIER = { 0xAD, 0xDE, 0xAD, 0xDE };

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

    private static FixedArray<byte> CreateMiloFile(MiloDirectorySpec root)
    {
        using var payload = new MemoryStream();
        WriteDirectory(payload, root);
        byte[] payloadBytes = payload.ToArray();

        using var milo = new MemoryStream();
        milo.Write(0xCABEDEAFu, Endianness.Little);
        milo.Write(20u, Endianness.Little);
        milo.Write(1u, Endianness.Little);
        milo.Write((uint) payloadBytes.Length, Endianness.Little);
        milo.Write((uint) payloadBytes.Length, Endianness.Little);
        milo.Write(payloadBytes);

        byte[] miloBytes = milo.ToArray();
        var buffer = FixedArray<byte>.Alloc(miloBytes.Length);
        miloBytes.CopyTo(buffer.Span);
        return buffer;
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

        stream.WriteByte(1);
        stream.WriteByte(0);

        foreach (var child in spec.SubDirectories)
        {
            WriteDirectory(stream, child);
        }

        stream.WriteByte(0x42);
        stream.Write(BARRIER);

        for (int i = 0; i < spec.Files.Count; i++)
        {
            var file = spec.Files[i];
            stream.Write(file.Data);

            bool isLastFile = i == spec.Files.Count - 1;
            if (!(isLastFile && spec.OmitFinalBarrierForLastFile))
            {
                stream.Write(BARRIER);
            }
        }
    }

    private static void WriteBigEndianString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.Write((uint) bytes.Length, Endianness.Big);
        stream.Write(bytes);
    }

    private sealed class MiloDirectorySpec
    {
        public string Name { get; init; } = "dir";

        public bool IncludeOptionalFields { get; init; }

        public bool OmitFinalBarrierForLastFile { get; init; }

        public List<MiloFileSpec> Files { get; } = new();

        public List<MiloDirectorySpec> SubDirectories { get; } = new();
    }

    private sealed record MiloFileSpec(string Name, string Value, byte[] Data);
}
