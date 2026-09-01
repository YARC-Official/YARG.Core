using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;
using ChartFormat = YARG.Core.Song.ChartFormat;

namespace YARG.Core.UnitTests.Song;

public class UltraStarIniEntryTests
{
    [SetUp]
    public void SetUp()
    {
        // ScanUltraStar falls back to audio-duration lookup for SongLength, which
        // otherwise throws if no audio backend has ever been initialized.
        GlobalAudioHandler.Initialize<NullAudioManager>();
    }

    [Test]
    public void DiscoversChartFileNotNamedNotesTxt()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "Some Artist - Some Title.txt", CreateBasicUsChart());
            Assert.That(entry.Name.Original, Is.EqualTo("Test Song"));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void DiscoversChartFileViaFileCollectionExtensionFallback()
    {
        // Mirrors what CacheHandler.ScanIniEntry does: fall back to extension search
        // when the fixed "notes.txt" name isn't present.
        string root = CreateTempDirectory();
        string songDir = Path.Combine(root, "song");
        Directory.CreateDirectory(songDir);
        try
        {
            string chartPath = Path.Combine(songDir, "Artist - Title.txt");
            File.WriteAllText(chartPath, CreateBasicUsChart());
            File.WriteAllBytes(Path.Combine(songDir, "audio.mp3"), new byte[] { 0x00 });

            var collection = new FileCollection(new DirectoryInfo(songDir));

            Assert.That(collection.FindFile("notes.txt", out _), Is.False);
            var found = collection.FindAllFilesByExtension(".txt");
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Name, Is.EqualTo("Artist - Title.txt"));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void ExtensionFallbackFindsEveryTxtFileForMultiSongFolders()
    {
        string root = CreateTempDirectory();
        string songDir = Path.Combine(root, "song");
        Directory.CreateDirectory(songDir);
        try
        {
            File.WriteAllText(Path.Combine(songDir, "Artist - Title.txt"), CreateBasicUsChart());
            File.WriteAllText(Path.Combine(songDir, "Artist - Other Title.txt"), CreateBasicUsChart());

            var collection = new FileCollection(new DirectoryInfo(songDir));

            var found = collection.FindAllFilesByExtension(".txt");
            Assert.That(found.Select(f => f.Name), Is.EquivalentTo(new[]
            {
                "Artist - Title.txt", "Artist - Other Title.txt"
            }));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void FolderWithMultipleTxtFilesScansEachAsItsOwnSong()
    {
        // A folder holding more than one UltraStar .txt (e.g. two songs sharing a pack)
        // should produce one entry per chart, not fail discovery outright.
        string root = CreateTempDirectory();
        string songDir = Path.Combine(root, "songs");
        Directory.CreateDirectory(songDir);
        try
        {
            File.WriteAllText(Path.Combine(songDir, "Artist - First.txt"),
                CreateBasicUsChart().Replace("Test Song", "First Song").Replace("audio.mp3", "first.mp3"));
            File.WriteAllBytes(Path.Combine(songDir, "first.mp3"), new byte[] { 0x00 });

            File.WriteAllText(Path.Combine(songDir, "Artist - Second.txt"),
                CreateBasicUsChart().Replace("Test Song", "Second Song").Replace("audio.mp3", "second.mp3"));
            File.WriteAllBytes(Path.Combine(songDir, "second.mp3"), new byte[] { 0x00 });

            string cachePath = Path.Combine(root, "songcache.bin");
            string badSongsPath = Path.Combine(root, "badsongs.txt");
            var cache = CacheHandler.RunScan(false, cachePath, badSongsPath, false, new List<string> { songDir });

            var names = cache.Entries.Values
                .SelectMany(list => list)
                .Select(entry => entry.Name.Original)
                .OrderBy(name => name)
                .ToList();
            Assert.That(names, Is.EqualTo(new[] { "First Song", "Second Song" }));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void ResolvesVideoFromTagRatherThanFixedStemName()
    {
        // LoadBackground opens the video file directly (no image decoding needed),
        // so this exercises tag-driven resolution without a real media fixture.
        // Cover/background follow the same GetSubFiles() lookup (see CacheHandler
        // skill notes) but require a decodable image to assert on, so aren't
        // covered by a lightweight unit test here.
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(
                extraTags: "#VIDEO:clip.mp4\n"),
                extraFiles: new[] { "clip.mp4" });

            using var background = entry.LoadBackground(false);
            Assert.That(background, Is.Not.Null);
            Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void ResolvesAudioFromMp3TagNotFixedStemName()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(),
                audioFileName: "whatever_the_author_named_it.mp3");

            // Successful scan already proves audio was resolved (ScanUltraStar returns
            // NoAudio otherwise) -- LoadAudio needs a real playable file so isn't exercised here.
            Assert.That(entry, Is.Not.Null);
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void FailsScanWhenTaggedAudioFileIsMissing()
    {
        string root = CreateTempDirectory();
        string songDir = Path.Combine(root, "song");
        Directory.CreateDirectory(songDir);
        try
        {
            string chartPath = Path.Combine(songDir, "song.txt");
            File.WriteAllText(chartPath, CreateBasicUsChart());
            // Deliberately do not create "audio.mp3" referenced by #MP3.

            var result = UnpackedIniEntry.ProcessNewEntry(songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "");
            Assert.That(result.HasValue, Is.False);
            Assert.That(result.Error, Is.EqualTo(ScanResult.NoAudio));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void GapPropagatesToSongOffsetMilliseconds()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(extraTags: "#GAP:2500\n"));
            Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(2500));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void VideoGapConvertsSecondsToVideoStartMilliseconds()
    {
        string root = CreateTempDirectory();
        try
        {
            // VIDEOGAP delays the video's start relative to the song; Video.Start is consumed
            // as a seek offset (positive = skip ahead), so the delay maps to a negative value.
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(extraTags: "#VIDEOGAP:1.5\n"));
            Assert.That(entry.VideoStartTimeMilliseconds, Is.EqualTo(-1500));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void CommentMapsToLoadingPhrase()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(extraTags: "#COMMENT:Sing loud!\n"));
            Assert.That(entry.LoadingPhrase, Is.EqualTo("Sing loud!"));
        }
        finally
        {
            CleanUp(root);
        }
    }

    [Test]
    public void AuthorTagFallsBackWhenCreatorMissing()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateUltraStarEntryViaScan(root, "song.txt", CreateBasicUsChart(extraTags: "#AUTHOR:Some Author\n"));
            Assert.That(entry.Charter.Original, Is.EqualTo("Some Author"));
        }
        finally
        {
            CleanUp(root);
        }
    }

    private static UnpackedIniEntry CreateUltraStarEntryViaScan(string root, string chartFileName, string chartContent,
        string audioFileName = "audio.mp3", string[]? extraFiles = null)
    {
        string songDir = Path.Combine(root, "song");
        Directory.CreateDirectory(songDir);

        string chartPath = Path.Combine(songDir, chartFileName);
        File.WriteAllText(chartPath, chartContent.Replace("audio.mp3", audioFileName));
        File.WriteAllBytes(Path.Combine(songDir, audioFileName), new byte[] { 0x00 });

        if (extraFiles != null)
        {
            foreach (var file in extraFiles)
            {
                File.WriteAllBytes(Path.Combine(songDir, file), new byte[] { 0x00 });
            }
        }

        var result = UnpackedIniEntry.ProcessNewEntry(songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "");
        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
        return result.Value;
    }

    private static string CreateBasicUsChart(string extraTags = "")
    {
        return "#TITLE:Test Song\n" +
               "#ARTIST:Test Artist\n" +
               "#MP3:audio.mp3\n" +
               "#BPM:120\n" +
               extraTags +
               ": 0 4 0 Hello\n" +
               "E\n";
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"yarg-us-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanUp(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
