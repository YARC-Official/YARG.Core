using NUnit.Framework;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Song;

public class FileCollectionTests
{
    [Test]
    public void Constructor_CapturesDirectoryAndFindsLowercasedEntries()
    {
        string path = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(path, "SONG.OGG"), string.Empty);
            Directory.CreateDirectory(Path.Combine(path, "AlbumArt"));

            var collection = new FileCollection(new DirectoryInfo(path));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(collection.Directory, Is.EqualTo(path));
                Assert.That(collection.ContainedDupes, Is.False);
                Assert.That(collection.FindFile("song.ogg", out var file), Is.True);
                Assert.That(file.Name, Is.EqualTo("SONG.OGG"));
                Assert.That(collection.FindDirectory("albumart", out var directory), Is.True);
                Assert.That(directory.Name, Is.EqualTo("AlbumArt"));
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void FindFileAndFindDirectory_ReturnFalseWhenTypeDoesNotMatchOrEntryIsMissing()
    {
        string path = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(path, "notes.mid"), string.Empty);
            Directory.CreateDirectory(Path.Combine(path, "subdir"));

            var collection = new FileCollection(new DirectoryInfo(path));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(collection.FindDirectory("notes.mid", out _), Is.False);
                Assert.That(collection.FindFile("subdir", out _), Is.False);
                Assert.That(collection.FindFile("missing.file", out _), Is.False);
                Assert.That(collection.FindDirectory("missingdir", out _), Is.False);
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void ContainsDirectoryAndContainsAudio_ReflectCurrentEntries()
    {
        string path = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(path, "GUITAR.MP3"), string.Empty);
            File.WriteAllText(Path.Combine(path, "readme.txt"), string.Empty);
            Directory.CreateDirectory(Path.Combine(path, "subdir"));

            var collection = new FileCollection(new DirectoryInfo(path));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(collection.ContainsDirectory(), Is.True);
                Assert.That(collection.ContainsAudio(), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void ContainsAudio_ReturnsFalseWhenNoSupportedAudioFilesArePresent()
    {
        string path = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(path, "notes.mid"), string.Empty);
            File.WriteAllText(Path.Combine(path, "preview.txt"), string.Empty);

            var collection = new FileCollection(new DirectoryInfo(path));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(collection.ContainsDirectory(), Is.False);
                Assert.That(collection.ContainsAudio(), Is.False);
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void Enumerator_UsesLowercasedKeys()
    {
        string path = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(path, "VoCaLs_1.OGG"), string.Empty);
            File.WriteAllText(Path.Combine(path, "Notes.mid"), string.Empty);

            var collection = new FileCollection(new DirectoryInfo(path));

            var keys = collection.Select(node => node.Key).OrderBy(key => key).ToArray();

            Assert.That(keys, Is.EqualTo(new[] { "notes.mid", "vocals_1.ogg" }));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"yarg-filecollection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
