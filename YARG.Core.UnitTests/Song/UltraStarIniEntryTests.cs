using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;
using ChartFormat = YARG.Core.Song.ChartFormat;

namespace YARG.Core.UnitTests.Song;

public class UltraStarIniEntryTests
{
    // "café" composed (NFC, single U+00E9) vs decomposed (NFD, 'e' + U+0301). Written as
    // escapes so the distinction survives an editor normalizing this file.
    private const string COMPOSED_NAME = "caf\u00E9";
    private const string DECOMPOSED_NAME = "cafe\u0301";

    private string _root = null!;
    private string _songDir = null!;

    [SetUp]
    public void SetUp()
    {
        // ScanUltraStar falls back to audio-duration lookup for SongLength, which
        // otherwise throws if no audio backend has ever been initialized.
        GlobalAudioHandler.Initialize<NullAudioManager>();

        _root = Path.Combine(Path.GetTempPath(), $"yarg-us-{Guid.NewGuid():N}");
        _songDir = Path.Combine(_root, "song");
        Directory.CreateDirectory(_songDir);
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
    public void DiscoversChartFileNotNamedNotesTxt()
    {
        var entry = Scan("Some Artist - Some Title.txt");
        Assert.That(entry.Name.Original, Is.EqualTo("Test Song"));
    }

    [Test]
    public void FolderWithMultipleTxtFilesScansEachAsItsOwnSong()
    {
        // A folder holding more than one UltraStar .txt (e.g. two songs sharing a pack)
        // should produce one entry per chart, not fail discovery outright.
        WriteChart("Artist - First.txt", BasicChart(title: "First Song", audio: "first.mp3"));
        WriteAudio("first.mp3");
        WriteChart("Artist - Second.txt", BasicChart(title: "Second Song", audio: "second.mp3"));
        WriteAudio("second.mp3");

        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "First Song", "Second Song" }));
    }

    [Test]
    public void StrayTextFilesAreNotTreatedAsCharts()
    {
        // Packs routinely ship a readme/licence next to the chart; those must neither
        // scan as songs nor be reported as bad ones.
        WriteChart("Artist - Song.txt", BasicChart());
        WriteAudio("audio.mp3");
        File.WriteAllText(Path.Combine(_songDir, "readme.txt"), "Thanks for downloading!\nEnjoy.\n");

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.EqualTo(new[] { "Test Song" }));
        Assert.That(File.Exists(badSongsPath) && File.ReadAllText(badSongsPath).Contains("readme"), Is.False,
            "A stray readme.txt should not be reported as a bad song.");
    }

    [Test]
    public void ResolvesVideoFromTagRatherThanFixedStemName()
    {
        // LoadBackground opens the video file directly (no image decoding needed), so this
        // exercises tag-driven resolution without a real media fixture. Cover/background
        // use the same GetSubFiles() lookup but need a decodable image to assert on.
        var entry = Scan(chart: BasicChart(extraTags: "#VIDEO:clip.mp4"), extraFiles: "clip.mp4");

        using var background = entry.LoadBackground(false);
        Assert.That(background, Is.Not.Null);
        Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
    }

    [TestCase("whatever_the_author_named_it.mp3", TestName = "Audio resolves from the tag, not a fixed stem name")]
    [TestCase("audio.mp3", TestName = "Audio resolves when the tag happens to match a stem name")]
    public void ResolvesAudioFromTagNotFixedStemName(string audioFileName)
    {
        // A successful scan is itself the assertion: ScanUltraStar returns NoAudio when the
        // tagged file can't be found (see FailsScanWhenTaggedAudioFileIsMissing).
        Assert.That(Scan(chart: BasicChart(audio: audioFileName), audio: audioFileName), Is.Not.Null);
    }

    [Test]
    public void FailsScanWhenTaggedAudioFileIsMissing()
    {
        // Deliberately do not create the "audio.mp3" the chart references.
        string chartPath = WriteChart("song.txt", BasicChart());

        var result = UnpackedIniEntry.ProcessNewEntry(_songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "");

        Assert.That(result.HasValue, Is.False);
        Assert.That(result.Error, Is.EqualTo(ScanResult.NoAudio));
    }

    [Test]
    public void ResolvesAudioAcrossUnicodeNormalizationForms()
    {
        // macOS' filesystem APIs return decomposed names for accented files, while chart
        // tags are typically composed. Both must resolve to the same file.
        Assert.That(COMPOSED_NAME, Is.Not.EqualTo(DECOMPOSED_NAME), "Sanity check: forms must differ byte-for-byte.");

        string audio = DECOMPOSED_NAME + ".mp3";
        WriteAudio(audio);
        string chartPath = WriteChart("song.txt", BasicChart(audio: COMPOSED_NAME + ".mp3"));

        var result = UnpackedIniEntry.ProcessNewEntry(_songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "");
        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
    }

    [Test]
    public void ResolvesVideoAcrossUnicodeNormalizationForms()
    {
        // Same NFC/NFD mismatch as the audio case, but through GetSubFiles() rather than
        // the scan-time existence check -- a separate code path.
        var entry = Scan(chart: BasicChart(extraTags: $"#VIDEO:{COMPOSED_NAME}.mp4"),
            extraFiles: DECOMPOSED_NAME + ".mp4");

        using var background = entry.LoadBackground(false);
        Assert.That(background, Is.Not.Null);
        Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
    }

    [Test]
    public void GapDoesNotAlsoSetSongOffset()
    {
        // GAP is already baked into the chart's own note ticks by UltraStarLoader (see
        // UltraStarLoaderTests.Basic.cs's GapShiftsFirstNoteByExactlyOneGapNotTwo) --
        // SongOffset must stay untouched, or playback gets delayed by 2x GAP.
        var entry = Scan(chart: BasicChart(extraTags: "#GAP:2500"));
        Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(0));
    }

    // VIDEOGAP is a seek offset into the video, not a playback delay -- which is what
    // Video.Start means too, so it maps across without a sign flip. US files routinely
    // use comma decimals.
    [TestCase("1.5", 1500, TestName = "VIDEOGAP seconds convert to milliseconds")]
    [TestCase("1,5", 1500, TestName = "VIDEOGAP accepts comma decimals")]
    [TestCase("80.2", 80200, TestName = "VIDEOGAP handles a long skip into the video")]
    public void VideoGapConvertsSecondsToVideoStartMilliseconds(string tagValue, long expectedMs)
    {
        var entry = Scan(chart: BasicChart(extraTags: $"#VIDEOGAP:{tagValue}"));
        Assert.That(entry.VideoStartTimeMilliseconds, Is.EqualTo(expectedMs));
    }

    [TestCase("#COMMENT:Sing loud!", "Sing loud!", TestName = "COMMENT maps to the loading phrase")]
    [TestCase("", "", TestName = "Loading phrase is empty without COMMENT")]
    public void CommentMapsToLoadingPhrase(string extraTags, string expected)
    {
        var entry = Scan(chart: BasicChart(extraTags: extraTags));
        Assert.That(entry.LoadingPhrase, Is.EqualTo(expected));
    }

    [TestCase("#AUTHOR:Some Author", "Some Author", TestName = "AUTHOR fills Charter when CREATOR is absent")]
    [TestCase("#CREATOR:Real Creator\n#AUTHOR:Some Author", "Real Creator", TestName = "CREATOR wins over AUTHOR")]
    public void CharterFallsBackFromCreatorToAuthor(string extraTags, string expected)
    {
        var entry = Scan(chart: BasicChart(extraTags: extraTags));
        Assert.That(entry.Charter.Original, Is.EqualTo(expected));
    }

    // #EDITION is US's closest equivalent to FoF's ini "icon" key, which feeds the
    // game/pack icon in the library UI.
    [TestCase("#EDITION:SingStar Party", "SingStar Party", TestName = "EDITION maps to Source")]
    [TestCase("", SongMetadata.DEFAULT_SOURCE, TestName = "Source defaults without EDITION")]
    [TestCase("#EDITION:", SongMetadata.DEFAULT_SOURCE, TestName = "A blank EDITION does not clobber the default")]
    public void EditionMapsToSource(string extraTags, string expected)
    {
        var entry = Scan(chart: BasicChart(extraTags: extraTags));
        Assert.That(entry.Source.Original, Is.EqualTo(expected));
    }

    /// <summary>Writes a chart plus its audio and scans it, asserting the scan succeeds.</summary>
    private UnpackedIniEntry Scan(string chartFileName = "song.txt", string? chart = null,
        string audio = "audio.mp3", params string[] extraFiles)
    {
        string chartPath = WriteChart(chartFileName, chart ?? BasicChart(audio: audio));
        WriteAudio(audio);
        foreach (string file in extraFiles)
        {
            WriteAudio(file);
        }

        var result = UnpackedIniEntry.ProcessNewEntry(_songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "");
        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
        return result.Value;
    }

    private string WriteChart(string fileName, string content)
    {
        string path = Path.Combine(_songDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private void WriteAudio(string fileName)
        => File.WriteAllBytes(Path.Combine(_songDir, fileName), new byte[] { 0x00 });

    /// <summary>Runs a full library scan over the song folder and returns the entry names.</summary>
    private string[] ScanFolderForNames(string? badSongsPath = null)
    {
        var cache = CacheHandler.RunScan(false,
            Path.Combine(_root, "songcache.bin"),
            badSongsPath ?? Path.Combine(_root, "badsongs.txt"),
            false,
            new List<string> { _songDir });

        return cache.Entries.Values
            .SelectMany(list => list)
            .Select(entry => entry.Name.Original)
            .OrderBy(name => name)
            .ToArray();
    }

    private static string BasicChart(string title = "Test Song", string audio = "audio.mp3", string extraTags = "")
    {
        string tags = extraTags.Length > 0 ? extraTags.TrimEnd('\n') + "\n" : string.Empty;
        return $"#TITLE:{title}\n" +
               "#ARTIST:Test Artist\n" +
               $"#MP3:{audio}\n" +
               "#BPM:120\n" +
               tags +
               ": 0 4 0 Hello\n" +
               "E\n";
    }
}
