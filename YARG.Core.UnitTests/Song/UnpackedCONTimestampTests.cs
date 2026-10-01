using System.Reflection;
using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Song;

// The scanner records max(LastWriteTime, CreationTime) for each file, so anything copied in a way that
// stamps a fresh creation time (File.Copy, Explorer, robocopy) must still validate against that value.
public class UnpackedCONTimestampTests
{
    private const string NODE_NAME = "testsong";
    private const string TITLE = "Timestamp Song";

    private string _root = null!;
    private string _library = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"yarg-con-mtime-{Guid.NewGuid():N}");
        _library = Path.Combine(_root, "library");
        Directory.CreateDirectory(_library);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Test]
    public void UnpackedRBCONEntry_ValidatesMidiCreatedAfterLastWrite()
    {
        string songsDirectory = CreateUnpackedCon();
        var root = new AbridgedFileInfo(songsDirectory, DateTime.MinValue);
        var stream = SerializeScannedEntry<UnpackedRBCONEntry>(out var data);
        using (data)
        {
            var restored = UnpackedRBCONEntry.TryDeserialize(in root, NODE_NAME, ref stream, CreateStrings());
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Name.Original, Is.EqualTo(TITLE));
        }
    }

    [Test]
    public void UnpackedRBPKGEntry_ValidatesMidiCreatedAfterLastWrite()
    {
        string songsDirectory = CreateUnpackedCon();
        string midiPath = Path.Combine(songsDirectory, NODE_NAME, NODE_NAME + ".mid");
        File.Move(midiPath, midiPath + ".edat");

        var root = new AbridgedFileInfo(songsDirectory, DateTime.MinValue);
        var stream = SerializeScannedEntry<UnpackedRBPKGEntry>(out var data);
        using (data)
        {
            var restored = UnpackedRBPKGEntry.TryDeserialize(in root, NODE_NAME, ref stream, CreateStrings());
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Name.Original, Is.EqualTo(TITLE));
        }
    }

    private string CreateUnpackedCon()
    {
        string songsDirectory = Path.Combine(_library, "unpacked", "songs");
        string songDirectory = Path.Combine(songsDirectory, NODE_NAME);
        Directory.CreateDirectory(songDirectory);
        File.WriteAllBytes(Path.Combine(songsDirectory, CONEntryGroup.SONGS_DTA), CreateDta());
        File.WriteAllBytes(Path.Combine(songDirectory, NODE_NAME + ".mogg"), BitConverter.GetBytes(RBCONEntry.UNENCRYPTED_MOGG));

        string midiPath = Path.Combine(songDirectory, NODE_NAME + ".mid");
        File.WriteAllBytes(midiPath, File.ReadAllBytes(GetTestMidiPath()));
        File.SetLastWriteTime(midiPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local));
        File.SetCreationTime(midiPath, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Local));

        var info = new FileInfo(midiPath);
        if (info.CreationTime <= info.LastWriteTime)
        {
            Assert.Ignore("This file system cannot set a creation time later than the last write time.");
        }
        return songsDirectory;
    }

    private FixedArrayStream SerializeScannedEntry<TEntry>(out FixedArray<byte> data)
        where TEntry : SongEntry
    {
        var cache = CacheHandler.RunScan(false, Path.Combine(_root, "songcache.bin"), Path.Combine(_root, "badsongs.txt"),
            false, new List<string> { _library });
        var entries = cache.Entries.Values.SelectMany(list => list).ToArray();
        Assert.That(entries, Has.Length.EqualTo(1));
        Assert.That(entries[0], Is.InstanceOf<TEntry>());

        using var buffer = new MemoryStream();
        entries[0].Serialize(buffer, new CacheWriteIndices());
        buffer.Position = 0;
        data = FixedArray.ReadRemainder(buffer);
        return data.ToValueStream();
    }

    // CacheReadStrings only has a constructor that parses a cache through a pointer, which would need
    // unsafe code in this project. Every CacheWriteIndices field above is zero, so one string per category
    // is enough.
    private static CacheReadStrings CreateStrings()
    {
        var categories = new string[CacheReadStrings.NUM_CATEGORIES][];
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i] = new[] { TITLE };
        }

        var strings = (CacheReadStrings) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CacheReadStrings));
        typeof(CacheReadStrings)
            .GetField("_categories", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(strings, categories);
        return strings;
    }

    private static byte[] CreateDta()
    {
        return Encoding.UTF8.GetBytes($$"""
            ({{NODE_NAME}}
              (name "{{TITLE}}")
              (song
                (name "songs/{{NODE_NAME}}/{{NODE_NAME}}")
                (pans (0.0))
                (vols (0.0))
                (cores (0.0))
              )
            )
            """);
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
