using System.Text;
using NUnit.Framework;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO.ConHandler;

public class ConHandlerTests
{
    private const int MetadataPosition = 0x340;
    private const int FileTableBlockCountPosition = 0x37C;
    private const int FileTableFirstBlockPosition = 0x37E;
    private const int SizeOfFileListing = 0x40;

    [Test]
    public void TryParseListings_ParsesDirectoryHierarchyAndContiguousFile()
    {
        byte[] expected = "con-data"u8.ToArray();
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(2, 0) + CONFileStream.BYTES_PER_BLOCK);

        expected.CopyTo(image, CONFileStream.CalculateBlockLocation(0, 0));
        WriteMetadata(image, entryId: 0x0000B000, fileTableBlockCount: 1, fileTableFirstBlock: 2);

        int tableOffset = (int) CONFileStream.CalculateBlockLocation(2, 0);
        WriteListing(image.AsSpan(tableOffset, SizeOfFileListing), "songs", CONFileListing.Flag.Directory, 0, 0, -1, 0);
        WriteListing(image.AsSpan(tableOffset + SizeOfFileListing, SizeOfFileListing),
            "track.mid", CONFileListing.Flag.Consecutive, 1, 0, 0, expected.Length);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Not.Null);
            Assert.That(listings, Has.Count.EqualTo(2));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(listings![0].Name, Is.EqualTo("songs"));
                Assert.That(listings[0].IsDirectory(), Is.True);

                Assert.That(listings.FindListing("songs/track.mid", out var fileListing), Is.True);
                Assert.That(fileListing.Name, Is.EqualTo("songs/track.mid"));
                Assert.That(fileListing.IsDirectory(), Is.False);
                Assert.That(fileListing.IsContiguous(), Is.True);
                Assert.That(fileListing.PathIndex, Is.Zero);
            }

            using var dataStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var file = CONFileStream.LoadFile(dataStream, listings[1]);
            Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryParseListings_WhenPathIndexIsOutOfRange_ReturnsNull()
    {
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(1, 0) + CONFileStream.BYTES_PER_BLOCK);
        WriteMetadata(image, entryId: 0x0000B000, fileTableBlockCount: 1, fileTableFirstBlock: 1);

        int tableOffset = (int) CONFileStream.CalculateBlockLocation(1, 0);
        WriteListing(image.AsSpan(tableOffset, SizeOfFileListing),
            "bad.mid", CONFileListing.Flag.Consecutive, 1, 0, 5, 16);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadFile_ReadsSplitFileUsingHashChain()
    {
        byte[] expected = Enumerable.Range(0, 5000).Select(i => (byte) (i % 251)).ToArray();
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(2, 0) + CONFileStream.BYTES_PER_BLOCK);

        expected.AsSpan(0, CONFileStream.BYTES_PER_BLOCK)
            .CopyTo(image.AsSpan((int) CONFileStream.CalculateBlockLocation(1, 0), CONFileStream.BYTES_PER_BLOCK));
        expected.AsSpan(CONFileStream.BYTES_PER_BLOCK)
            .CopyTo(image.AsSpan((int) CONFileStream.CalculateBlockLocation(2, 0), expected.Length - CONFileStream.BYTES_PER_BLOCK));

        int nextBlockHashIndex = 1 * CONFileStream.BYTES_PER_HASH_ENTRY + CONFileStream.NEXT_BLOCK_HASH_OFFSET;
        int hashBlockLocation = (int) (CONFileStream.CalculateBlockLocation(1, 0) - ((1 + 1) * CONFileStream.BYTES_PER_BLOCK));
        image[hashBlockLocation + nextBlockHashIndex] = 0x00;
        image[hashBlockLocation + nextBlockHashIndex + 1] = 0x00;
        image[hashBlockLocation + nextBlockHashIndex + 2] = 0x02;

        var listing = new CONFileListing
        {
            Name = "split.mid",
            Flags = 0,
            BlockCount = 2,
            BlockOffset = 1,
            PathIndex = -1,
            Length = expected.Length,
            Shift = 0,
        };

        using var stream = new MemoryStream(image, writable: false);
        using var file = CONFileStream.LoadFile(stream, listing);

        Assert.That(file.ReadOnlySpan.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void CreateStream_ReadsAndSeeksAcrossContiguousSectionBoundary()
    {
        byte[] expected = Enumerable.Range(0, CONFileStream.BYTES_PER_BLOCK + 128)
            .Select(i => (byte) (i % 251))
            .ToArray();
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(CONFileStream.BLOCKS_PER_SECTION, 0)
            + CONFileStream.BYTES_PER_BLOCK);

        expected.AsSpan(0, CONFileStream.BYTES_PER_BLOCK).CopyTo(
            image.AsSpan((int) CONFileStream.CalculateBlockLocation(CONFileStream.BLOCKS_PER_SECTION - 1, 0),
                CONFileStream.BYTES_PER_BLOCK));
        expected.AsSpan(CONFileStream.BYTES_PER_BLOCK).CopyTo(
            image.AsSpan((int) CONFileStream.CalculateBlockLocation(CONFileStream.BLOCKS_PER_SECTION, 0),
                expected.Length - CONFileStream.BYTES_PER_BLOCK));

        var listing = new CONFileListing
        {
            Name = "boundary.mid",
            Flags = CONFileListing.Flag.Consecutive,
            BlockCount = 2,
            BlockOffset = CONFileStream.BLOCKS_PER_SECTION - 1,
            PathIndex = -1,
            Length = expected.Length,
            Shift = 0,
        };

        string path = CreateTempFile(image);
        try
        {
            using var stream = CONFileStream.CreateStream(path, listing);

            var firstRead = new byte[64];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(stream.Read(firstRead, 0, firstRead.Length), Is.EqualTo(firstRead.Length));
                Assert.That(firstRead, Is.EqualTo(expected[..64]));

                Assert.That(stream.Seek(CONFileStream.BYTES_PER_BLOCK - 32, SeekOrigin.Begin),
                    Is.EqualTo(CONFileStream.BYTES_PER_BLOCK - 32));
            }

            var boundaryRead = new byte[96];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(stream.Read(boundaryRead, 0, boundaryRead.Length), Is.EqualTo(boundaryRead.Length));
                Assert.That(boundaryRead,
                    Is.EqualTo(expected.AsSpan(CONFileStream.BYTES_PER_BLOCK - 32, 96).ToArray()));

                Assert.That(stream.Seek(-16, SeekOrigin.End), Is.EqualTo(expected.Length - 16));
            }

            var tail = new byte[16];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(stream.Read(tail, 0, tail.Length), Is.EqualTo(tail.Length));
                Assert.That(tail, Is.EqualTo(expected[^16..]));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryParseListings_ParsesShiftedPackageAndLoadsContiguousFile()
    {
        byte[] expected = Enumerable.Range(0, 96).Select(i => (byte) (255 - i)).ToArray();
        const int SHIFT = 1;
        const int FIRST_BLOCK = 1;

        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(2, SHIFT) + CONFileStream.BYTES_PER_BLOCK);
        expected.CopyTo(image, (int) CONFileStream.CalculateBlockLocation(0, SHIFT));
        WriteMetadata(image, entryId: 0x0000A000, fileTableBlockCount: 1, fileTableFirstBlock: FIRST_BLOCK);

        int tableOffset = (int) CONFileStream.CalculateBlockLocation(FIRST_BLOCK, SHIFT);
        WriteListing(image.AsSpan(tableOffset, SizeOfFileListing),
            "shifted.mid", CONFileListing.Flag.Consecutive, 1, 0, -1, expected.Length);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Not.Null);
            Assert.That(listings, Has.Count.EqualTo(1));
            Assert.That(listings![0].Shift, Is.EqualTo(1));

            using var dataStream = CONFileStream.CreateStream(path, listings[0]);
            var data = new byte[expected.Length];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(dataStream.Read(data, 0, data.Length), Is.EqualTo(data.Length));
                Assert.That(data, Is.EqualTo(expected));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCase("CON ")]
    [TestCase("LIVE")]
    [TestCase("PIRS")]
    public void TryParseListings_AcceptsSupportedPackageTags(string tag)
    {
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(1, 0) + CONFileStream.BYTES_PER_BLOCK, tag);
        WriteMetadata(image, entryId: 0x0000B000, fileTableBlockCount: 1, fileTableFirstBlock: 1);

        int tableOffset = (int) CONFileStream.CalculateBlockLocation(1, 0);
        WriteListing(image.AsSpan(tableOffset, SizeOfFileListing),
            "song.mid", CONFileListing.Flag.Consecutive, 1, 0, -1, 8);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Not.Null);
            Assert.That(listings, Has.Count.EqualTo(1));
            Assert.That(listings![0].Name, Is.EqualTo("song.mid"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryParseListings_ReturnsNullForUnsupportedTag()
    {
        byte[] image = CreateConImage(CONFileStream.CalculateBlockLocation(1, 0) + CONFileStream.BYTES_PER_BLOCK, "BAD!");
        WriteMetadata(image, entryId: 0x0000B000, fileTableBlockCount: 1, fileTableFirstBlock: 1);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryParseListings_ReturnsNullForFilesShorterThanFirstBlockOffset()
    {
        byte[] image = CreateConImage(CONFileStream.FIRSTBLOCK_OFFSET);

        string path = CreateTempFile(image);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var listings = CONFile.TryParseListings(path, stream);

            Assert.That(listings, Is.Null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCase(0, 0, CONFileStream.FIRSTBLOCK_OFFSET)]
    [TestCase(CONFileStream.BLOCKS_PER_SECTION, 0,
        CONFileStream.FIRSTBLOCK_OFFSET + (CONFileStream.BLOCKS_PER_SECTION + 2) * CONFileStream.BYTES_PER_BLOCK)]
    [TestCase(CONFileStream.BLOCKS_PER_SECTION, 1,
        CONFileStream.FIRSTBLOCK_OFFSET + (CONFileStream.BLOCKS_PER_SECTION + 4) * CONFileStream.BYTES_PER_BLOCK)]
    [TestCase(CONFileStream.NUM_BLOCKS_SQUARED, 0,
        CONFileStream.FIRSTBLOCK_OFFSET + (CONFileStream.NUM_BLOCKS_SQUARED + 173) * CONFileStream.BYTES_PER_BLOCK)]
    public void CalculateBlockLocation_AccountsForSectionAndShiftAdjustments(int blockNum, int shift, long expected)
    {
        Assert.That(CONFileStream.CalculateBlockLocation(blockNum, shift), Is.EqualTo(expected));
    }

    private static byte[] CreateConImage(long length, string tag = "CON ")
    {
        var image = new byte[length];
        image[0] = (byte) tag[0];
        image[1] = (byte) tag[1];
        image[2] = (byte) tag[2];
        image[3] = (byte) tag[3];
        return image;
    }

    private static void WriteMetadata(byte[] image, int entryId, ushort fileTableBlockCount, int fileTableFirstBlock)
    {
        image[MetadataPosition] = (byte) (entryId >> 24);
        image[MetadataPosition + 1] = (byte) (entryId >> 16);
        image[MetadataPosition + 2] = (byte) (entryId >> 8);
        image[MetadataPosition + 3] = (byte) entryId;

        image[FileTableBlockCountPosition] = (byte) fileTableBlockCount;
        image[FileTableBlockCountPosition + 1] = (byte) (fileTableBlockCount >> 8);

        image[FileTableFirstBlockPosition] = (byte) (fileTableFirstBlock >> 16);
        image[FileTableFirstBlockPosition + 1] = (byte) (fileTableFirstBlock >> 8);
        image[FileTableFirstBlockPosition + 2] = (byte) fileTableFirstBlock;
    }

    private static void WriteListing(Span<byte> destination, string name, CONFileListing.Flag flags,
        int blockCount, int blockOffset, short pathIndex, int length)
    {
        destination.Clear();

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        nameBytes.CopyTo(destination);
        destination[0x28] = (byte) (((byte) flags) | nameBytes.Length);

        destination[0x29] = (byte) blockCount;
        destination[0x2A] = (byte) (blockCount >> 8);
        destination[0x2B] = (byte) (blockCount >> 16);

        destination[0x2F] = (byte) blockOffset;
        destination[0x30] = (byte) (blockOffset >> 8);
        destination[0x31] = (byte) (blockOffset >> 16);

        destination[0x32] = (byte) (pathIndex >> 8);
        destination[0x33] = (byte) pathIndex;

        destination[0x34] = (byte) (length >> 24);
        destination[0x35] = (byte) (length >> 16);
        destination[0x36] = (byte) (length >> 8);
        destination[0x37] = (byte) length;
    }

    private static string CreateTempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"yarg-conhandler-{Guid.NewGuid():N}.con");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}