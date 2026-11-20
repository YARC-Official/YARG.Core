using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Song;

public class CONGroupCacheTests
{
    private const string NODE_NAME = "testsong";
    private const string CACHED_TITLE = "Cached Song A";
    private const string EDITED_TITLE = "Cached Song B";

    private const int METADATA_POSITION = 0x340;
    private const int FILETABLE_BLOCKCOUNT_POSITION = 0x37C;
    private const int FILETABLE_FIRSTBLOCK_POSITION = 0x37E;
    private const int SIZEOF_FILELISTING = 0x40;

    private string _root = null!;
    private string _library = null!;
    private string _cachePath = null!;
    private string _badSongsPath = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"yarg-con-cache-{Guid.NewGuid():N}");
        _library = Path.Combine(_root, "library");
        Directory.CreateDirectory(_library);
        _cachePath = Path.Combine(_root, "songcache.bin");
        _badSongsPath = Path.Combine(_root, "badsongs.txt");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    // Each test rewrites the DTA after the first scan but restores its timestamp, so the cache still
    // considers the group current. A title from the edited DTA means the full scan discarded the cached
    // group and re-created its entries from the files.
    [Test]
    public void FullScan_RestoresPackedCONGroupFromCache()
    {
        string conPath = Path.Combine(_library, "pack", "testcon");
        Directory.CreateDirectory(Path.GetDirectoryName(conPath)!);
        File.WriteAllBytes(conPath, CreatePackedCon(CACHED_TITLE));

        Assert.That(GetTitles(RunFullScan()), Is.EqualTo(new[] { CACHED_TITLE }));

        var lastWrite = File.GetLastWriteTimeUtc(conPath);
        File.WriteAllBytes(conPath, CreatePackedCon(EDITED_TITLE));
        File.SetLastWriteTimeUtc(conPath, lastWrite);

        Assert.That(GetTitles(RunFullScan()), Is.EqualTo(new[] { CACHED_TITLE }));
    }

    [Test]
    public void FullScan_RestoresUnpackedCONGroupFromCache()
    {
        string songsDirectory = Path.Combine(_library, "unpacked", "songs");
        string songDirectory = Path.Combine(songsDirectory, NODE_NAME);
        Directory.CreateDirectory(songDirectory);
        File.WriteAllBytes(Path.Combine(songDirectory, NODE_NAME + ".mid"), File.ReadAllBytes(GetTestMidiPath()));
        File.WriteAllBytes(Path.Combine(songDirectory, NODE_NAME + ".mogg"), CreateMogg());

        string dtaPath = Path.Combine(songsDirectory, CONEntryGroup.SONGS_DTA);
        File.WriteAllBytes(dtaPath, CreateDta(CACHED_TITLE));

        Assert.That(GetTitles(RunFullScan()), Is.EqualTo(new[] { CACHED_TITLE }));

        var lastWrite = File.GetLastWriteTimeUtc(dtaPath);
        File.WriteAllBytes(dtaPath, CreateDta(EDITED_TITLE));
        File.SetLastWriteTimeUtc(dtaPath, lastWrite);

        Assert.That(GetTitles(RunFullScan()), Is.EqualTo(new[] { CACHED_TITLE }));
    }

    private SongCache RunFullScan()
    {
        return CacheHandler.RunScan(false, _cachePath, _badSongsPath, false, new List<string> { _library });
    }

    private static string[] GetTitles(SongCache cache)
    {
        return cache.Entries.Values.SelectMany(list => list).Select(entry => entry.Name.Original).ToArray();
    }

    private static byte[] CreateDta(string title)
    {
        return Encoding.UTF8.GetBytes($$"""
            ({{NODE_NAME}}
              (name "{{title}}")
              (song
                (name "songs/{{NODE_NAME}}/{{NODE_NAME}}")
                (pans (0.0))
                (vols (0.0))
                (cores (0.0))
              )
            )
            """);
    }

    private static byte[] CreateMogg()
    {
        return BitConverter.GetBytes(RBCONEntry.UNENCRYPTED_MOGG);
    }

    private static byte[] CreatePackedCon(string title)
    {
        const int FILETABLE_BLOCK = 0;
        const int DTA_BLOCK = 1;
        const int MIDI_BLOCK = 2;

        byte[] dta = CreateDta(title);
        byte[] midi = File.ReadAllBytes(GetTestMidiPath());
        byte[] mogg = CreateMogg();
        int midiBlocks = (midi.Length + CONFileStream.BYTES_PER_BLOCK - 1) / CONFileStream.BYTES_PER_BLOCK;
        int moggBlock = MIDI_BLOCK + midiBlocks;
        Assert.That(moggBlock, Is.LessThan(CONFileStream.BLOCKS_PER_SECTION), "Fixture must fit in one hash section");

        var image = new byte[CONFileStream.CalculateBlockLocation(moggBlock, 0) + CONFileStream.BYTES_PER_BLOCK];
        "CON "u8.CopyTo(image);

        // Entry ID 0xB000 selects the unshifted block layout
        image[METADATA_POSITION + 2] = 0xB0;
        image[FILETABLE_BLOCKCOUNT_POSITION] = 1;
        image[FILETABLE_FIRSTBLOCK_POSITION + 2] = FILETABLE_BLOCK;

        dta.CopyTo(image, CONFileStream.CalculateBlockLocation(DTA_BLOCK, 0));
        midi.CopyTo(image, CONFileStream.CalculateBlockLocation(MIDI_BLOCK, 0));
        mogg.CopyTo(image, CONFileStream.CalculateBlockLocation(moggBlock, 0));

        var table = image.AsSpan((int) CONFileStream.CalculateBlockLocation(FILETABLE_BLOCK, 0));
        WriteListing(table,"songs", CONFileListing.Flag.Directory, 0, 0, -1, 0);
        WriteListing(table[SIZEOF_FILELISTING..], CONEntryGroup.SONGS_DTA, CONFileListing.Flag.Consecutive, 1, DTA_BLOCK, 0, dta.Length);
        WriteListing(table[(2 * SIZEOF_FILELISTING)..], NODE_NAME, CONFileListing.Flag.Directory, 0, 0, 0, 0);
        WriteListing(table[(3 * SIZEOF_FILELISTING)..], NODE_NAME + ".mid", CONFileListing.Flag.Consecutive, midiBlocks, MIDI_BLOCK, 2, midi.Length);
        WriteListing(table[(4 * SIZEOF_FILELISTING)..], NODE_NAME + ".mogg", CONFileListing.Flag.Consecutive, 1, moggBlock, 2, mogg.Length);
        return image;
    }

    private static void WriteListing(Span<byte> destination, string name, CONFileListing.Flag flags,
        int blockCount, int blockOffset, short pathIndex, int length)
    {
        int nameLength = Encoding.UTF8.GetBytes(name, destination);
        destination[0x28] = (byte) ((byte) flags | nameLength);

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

    private static string GetTestMidiPath()
    {
        string path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../Parsing/Test Charts/test.mid"));
        Assert.That(File.Exists(path), Is.True, $"Expected test MIDI fixture at {path}.");
        return path;
    }
}
