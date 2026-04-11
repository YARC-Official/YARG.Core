using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongCacheCategoryTests
{
    [Test]
    public void WriteCategoriesToCache_RoundTripsCategoriesAndAssignsExpectedIndices()
    {
        var first = CreateEntry(
            name: "Song A",
            artist: "Artist",
            album: "Album",
            charter: "Charter",
            genre: "Rock",
            subgenre: "Alt Rock",
            year: "1999",
            playlist: "Playlist",
            source: "Source");
        var second = CreateEntry(
            name: "Song B",
            artist: "Artist",
            album: "Album",
            charter: "Charter",
            genre: "Rock",
            subgenre: "Alt Rock",
            year: "1999",
            playlist: "Playlist",
            source: "Source");

        var cache = new SongCache();
        cache.Entries.Add(
            HashWrapper.FromString("0000000000000000000000000000000000000001"),
            new List<SongEntry> { first, second });

        var (categories, nodes) = WriteAndReadCategories(cache);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(categories[0], Is.EqualTo(["Song A", "Song B"]));
            Assert.That(categories[1], Is.EqualTo(["Artist"]));
            Assert.That(categories[2], Is.EqualTo(["Album"]));
            Assert.That(categories[3], Is.EqualTo(["Rock"]));
            Assert.That(categories[4], Is.EqualTo(["Alt Rock"]));
            Assert.That(categories[5], Is.EqualTo(["1999"]));
            Assert.That(categories[6], Is.EqualTo(["Charter"]));
            Assert.That(categories[7], Is.EqualTo(["Playlist"]));
            Assert.That(categories[8], Is.EqualTo(["Source"]));

            Assert.That(nodes[first].Title, Is.Zero);
            Assert.That(nodes[second].Title, Is.EqualTo(1));
            Assert.That(nodes[first].Artist, Is.Zero);
            Assert.That(nodes[second].Artist, Is.Zero);
            Assert.That(nodes[first].Album, Is.Zero);
            Assert.That(nodes[second].Album, Is.Zero);
            Assert.That(nodes[first].Year, Is.Zero);
            Assert.That(nodes[second].Year, Is.Zero);
        }
    }

    [Test]
    public void WriteCategoriesToCache_DeduplicatesArtistsWithinOneHashGroupButNotAcrossGroups()
    {
        var first = CreateEntry(name: "Song A", artist: "Shared Artist");
        var second = CreateEntry(name: "Song B", artist: "Shared Artist");
        var third = CreateEntry(name: "Song C", artist: "Shared Artist");

        var cache = new SongCache();
        cache.Entries.Add(
            HashWrapper.FromString("0000000000000000000000000000000000000001"),
            new List<SongEntry> { first, second });
        cache.Entries.Add(
            HashWrapper.FromString("0000000000000000000000000000000000000002"),
            new List<SongEntry> { third });

        var (categories, nodes) = WriteAndReadCategories(cache);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(categories[1], Is.EqualTo(new[] { "Shared Artist", "Shared Artist" }));
            Assert.That(nodes[first].Artist, Is.Zero);
            Assert.That(nodes[second].Artist, Is.Zero);
            Assert.That(nodes[third].Artist, Is.EqualTo(1));
        }
    }

    private static TestSongEntry CreateEntry(
        string name = "Song Name",
        string artist = "Artist Name",
        string album = "Album Name",
        string charter = "Charter Name",
        string genre = "Genre Name",
        string subgenre = "Subgenre Name",
        string year = "2000",
        string playlist = "Playlist Name",
        string source = "Source Name")
    {
        var entry = new TestSongEntry();
        entry.SetMetadata(
            name: name,
            artist: artist,
            album: album,
            charter: charter,
            genre: genre,
            subgenre: subgenre,
            year: year,
            source: source,
            playlist: playlist);
        entry.SetLocations($"{name}.ini");
        return entry;
    }

    private static (string[][] Categories, Dictionary<SongEntry, CacheWriteIndices> Nodes) WriteAndReadCategories(SongCache cache)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        var nodes = new Dictionary<SongEntry, CacheWriteIndices>();

        try
        {
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                SongEntrySorting.WriteCategoriesToCache(output, cache, nodes);
            }

            return (ReadCategories(path), nodes);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string[][] ReadCategories(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, 0, bytes.Length, false, true);
        var categories = new string[CacheReadStrings.NUM_CATEGORIES][];

        for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
        {
            int sectionLength = stream.Read<int>(Endianness.Little);
            long sectionEnd = stream.Position + sectionLength;
            int stringCount = stream.Read<int>(Endianness.Little);
            var values = categories[categoryIndex] = new string[stringCount];

            for (int i = 0; i < stringCount; i++)
            {
                values[i] = stream.ReadString();
            }

            Assert.That(stream.Position, Is.EqualTo(sectionEnd));
        }

        Assert.That(stream.Position, Is.EqualTo(stream.Length));
        return categories;
    }
}