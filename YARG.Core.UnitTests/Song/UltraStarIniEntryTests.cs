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

    [TestCase("readme.txt", TestName = "readme.txt is skipped by the denylist")]
    [TestCase("README.txt", TestName = "README.txt is skipped case-insensitively")]
    [TestCase("License.txt", TestName = "License.txt is skipped by the denylist")]
    [TestCase("LICENCE.txt", TestName = "LICENCE.txt (British spelling) is skipped by the denylist")]
    [TestCase("COPYING.txt", TestName = "COPYING.txt is skipped by the denylist")]
    [TestCase("CHANGELOG.txt", TestName = "CHANGELOG.txt is skipped by the denylist")]
    [TestCase("info.txt", TestName = "An unrecognized name still falls back to the content check")]
    public void StrayTextFilesAreNotTreatedAsCharts(string strayFileName)
    {
        // Neither a scanned song nor a bad one, whether the name denylist or the content
        // check catches it.
        WriteChart("Artist - Song.txt", BasicChart());
        WriteAudio("audio.mp3");
        File.WriteAllText(Path.Combine(_songDir, strayFileName), "Thanks for downloading!\nEnjoy.\n");

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.EqualTo(new[] { "Test Song" }));
        bool reportedAsBad = File.Exists(badSongsPath) &&
            File.ReadAllText(badSongsPath).IndexOf(strayFileName, StringComparison.OrdinalIgnoreCase) >= 0;
        Assert.That(reportedAsBad, Is.False, $"A stray {strayFileName} should not be reported as a bad song.");
    }

    [Test]
    public void DenylistedNameIsSkippedEvenWithChartLikeContent()
    {
        // The denylist runs before the content check, so chart-like content doesn't matter.
        WriteChart("Artist - Song.txt", BasicChart());
        WriteAudio("audio.mp3");
        WriteChart("readme.txt", BasicChart(title: "Should Not Scan"));

        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "Test Song" }));
    }

    [Test]
    public void SongLengthComesFromTheAudioHeader()
    {
        // 1.5 s of 8 kHz mono 8-bit PCM. The test audio backend can't open anything, so a
        // non-zero length can only have come from the file's own header.
        var fmt = new byte[] { 1, 0, 1, 0, 0x40, 0x1F, 0, 0, 0x40, 0x1F, 0, 0, 1, 0, 8, 0 };
        var data = new byte[12000];
        using var wav = new MemoryStream();
        using (var writer = new BinaryWriter(wav, System.Text.Encoding.ASCII, true))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(4 + 8 + fmt.Length + 8 + data.Length);
            writer.Write("WAVEfmt ".ToCharArray());
            writer.Write(fmt.Length);
            writer.Write(fmt);
            writer.Write("data".ToCharArray());
            writer.Write(data.Length);
            writer.Write(data);
        }
        string chartPath = WriteChart("song.txt", BasicChart(audio: "audio.wav"));
        File.WriteAllBytes(Path.Combine(_songDir, "audio.wav"), wav.ToArray());

        var result = TryScan(chartPath);

        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
        Assert.That(result.Value.SongLengthMilliseconds, Is.EqualTo(1500));
    }

    [Test]
    public void SongLengthFallsBackToTheAudioBackendWithoutAUsableHeader()
    {
        // A one-byte "audio file" has no header to read, so the backend is asked as before --
        // and the test backend reports nothing.
        var entry = Scan(chart: BasicChart());
        Assert.That(entry.SongLengthMilliseconds, Is.EqualTo(0));
    }

    [Test]
    public void RescanPicksUpAChartAddedNextToACachedOne()
    {
        // ScanFolderForNames shares one songcache.bin, so the second scan loads "First Song"
        // from the cache -- which must not hide a variant added to the same folder since.
        WriteChart("Artist - First.txt", BasicChart(title: "First Song", audio: "first.mp3"));
        WriteAudio("first.mp3");
        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "First Song" }));

        WriteChart("Artist - Second.txt", BasicChart(title: "Second Song", audio: "second.mp3"));
        WriteAudio("second.mp3");
        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "First Song", "Second Song" }));
    }

    [Test]
    public void RescanPicksUpAnEditedChartNextToACachedOne()
    {
        WriteChart("Artist - First.txt", BasicChart(title: "First Song", audio: "first.mp3"));
        WriteAudio("first.mp3");
        string second = WriteChart("Artist - Second.txt", BasicChart(title: "Second Song", audio: "second.mp3"));
        WriteAudio("second.mp3");
        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "First Song", "Second Song" }));

        File.WriteAllText(second, BasicChart(title: "Second Song (Edited)", audio: "second.mp3"));
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddMinutes(1));
        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "First Song", "Second Song (Edited)" }));
    }

    [Test]
    public void MarkdownNotesInAPackFolderDoNotHideItsSongs()
    {
        // A notes file starting with a Markdown heading must not read as a broken chart: that
        // would report it and stop traversal into the pack's subfolders.
        File.WriteAllText(Path.Combine(_songDir, "info.txt"), "# Pack notes\nThanks for downloading!\n");
        string inner = Path.Combine(_songDir, "Artist - Song");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "Artist - Song.txt"), BasicChart());
        File.WriteAllBytes(Path.Combine(inner, "audio.mp3"), new byte[] { 0x00 });

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.EqualTo(new[] { "Test Song" }));
        bool reportedAsBad = File.Exists(badSongsPath) && File.ReadAllText(badSongsPath).Contains("info.txt");
        Assert.That(reportedAsBad, Is.False);
    }

    [Test]
    public void BrokenChartIsStillReportedAsABadSong()
    {
        // Has notes but no TITLE: a real chart that fails, so it must stay loud.
        File.WriteAllText(Path.Combine(_songDir, "Artist - Song.txt"), "#ARTIST:Someone\n#MP3:audio.mp3\n: 0 4 0 Hello\nE\n");
        WriteAudio("audio.mp3");

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.Empty);
        Assert.That(File.ReadAllText(badSongsPath), Does.Contain("Artist - Song.txt"));
    }

    [Test]
    public void FolderWithSongIniIsNeverScannedAsUltraStar()
    {
        // A song.ini marks an FoF/RB-style folder; only its fixed-name charts are looked
        // for, so a valid UltraStar .txt beside it is neither scanned nor reported.
        WriteChart("Artist - Song.txt", BasicChart());
        WriteAudio("audio.mp3");
        File.WriteAllText(Path.Combine(_songDir, "song.ini"), "[song]\nname = Ini Song\n");

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.Empty);
        bool reportedAsBad = File.Exists(badSongsPath) && File.ReadAllText(badSongsPath).Contains("Artist - Song.txt");
        Assert.That(reportedAsBad, Is.False);
    }

    [Test]
    public void ResolvesVideoFromTagRatherThanFixedStemName()
    {
        // A video needs no decoding to load, unlike the cover and background images, which
        // share the same lookup.
        var entry = Scan(chart: BasicChart(extraTags: "#VIDEO:clip.mp4"), extraFiles: "clip.mp4");

        using var background = entry.LoadBackground(false);
        Assert.That(background, Is.Not.Null);
        Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
    }

    [TestCase("whatever_the_author_named_it.mp3", TestName = "Audio resolves from the tag, not a fixed stem name")]
    [TestCase("audio.mp3", TestName = "Audio resolves when the tag happens to match a stem name")]
    // Most UltraStar charts ship .m4a, which IniAudio.SupportedFormats lacks but decodes fine.
    [TestCase("audio.m4a", TestName = "Audio resolves for a format outside IniAudio.SupportedFormats")]
    public void ResolvesAudioFromTagNotFixedStemName(string audioFileName)
    {
        // Scan asserts success, and a missing audio file fails it with NoAudio.
        Assert.That(Scan(chart: BasicChart(audio: audioFileName), audio: audioFileName), Is.Not.Null);
    }

    [Test]
    public void FailsScanWhenTaggedAudioFileIsMissing()
    {
        // Deliberately do not create the "audio.mp3" the chart references.
        string chartPath = WriteChart("song.txt", BasicChart());

        var result = TryScan(chartPath);

        Assert.That(result.HasValue, Is.False);
        Assert.That(result.Error, Is.EqualTo(ScanResult.NoAudio));
    }

    [Test]
    public void FailsScanWhenTaggedAudioIsAVideoFile()
    {
        // A video container can't be decoded as audio, so it must fail loudly at scan time.
        WriteAudio("song.mp4");
        string chartPath = WriteChart("song.txt", BasicChart(audio: "song.mp4"));

        var result = TryScan(chartPath);

        Assert.That(result.HasValue, Is.False);
        Assert.That(result.Error, Is.EqualTo(ScanResult.UnsupportedAudioFormat));
    }

    [Test]
    public void ResolvesAudioAcrossUnicodeNormalizationForms()
    {
        // macOS returns decomposed names for accented files; chart tags are usually composed.
        Assert.That(COMPOSED_NAME, Is.Not.EqualTo(DECOMPOSED_NAME), "Sanity check: forms must differ byte-for-byte.");

        string audio = DECOMPOSED_NAME + ".mp3";
        WriteAudio(audio);
        string chartPath = WriteChart("song.txt", BasicChart(audio: COMPOSED_NAME + ".mp3"));

        var result = TryScan(chartPath);
        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
    }

    [Test]
    public void ResolvesAudioAcrossUnicodeNormalizationFormsDuringFullScan()
    {
        // As above, but with the FileCollection a real library scan builds.
        string audio = DECOMPOSED_NAME + ".mp3";
        WriteAudio(audio);
        WriteChart("song.txt", BasicChart(audio: COMPOSED_NAME + ".mp3"));

        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "Test Song" }));
    }

    [Test]
    public void ResolvesVideoAcrossUnicodeNormalizationForms()
    {
        // Loading media resolves names through GetSubFiles, not the scan's FileCollection.
        var entry = Scan(chart: BasicChart(extraTags: $"#VIDEO:{COMPOSED_NAME}.mp4"),
            extraFiles: DECOMPOSED_NAME + ".mp4");

        using var background = entry.LoadBackground(false);
        Assert.That(background, Is.Not.Null);
        Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
    }

    [Test]
    public void GapDoesNotAlsoSetSongOffset()
    {
        // The note ticks already include GAP, so a SongOffset would apply it twice.
        var entry = Scan(chart: BasicChart(extraTags: "#GAP:2500"));
        Assert.That(entry.SongOffsetMilliseconds, Is.EqualTo(0));
    }

    // VIDEOGAP and Video.Start are both a seek offset into the video, so no sign flip.
    [TestCase("1.5", 1500, TestName = "VIDEOGAP seconds convert to milliseconds")]
    [TestCase("1,5", 1500, TestName = "VIDEOGAP accepts comma decimals")]
    [TestCase("80.2", 80200, TestName = "VIDEOGAP handles a long skip into the video")]
    public void VideoGapConvertsSecondsToVideoStartMilliseconds(string tagValue, long expectedMs)
    {
        var entry = Scan(chart: BasicChart(extraTags: $"#VIDEOGAP:{tagValue}"));
        Assert.That(entry.VideoStartTimeMilliseconds, Is.EqualTo(expectedMs));
    }

    // PREVIEWSTART is in seconds, like "47.16".
    [TestCase("47.16", 47160, TestName = "PREVIEWSTART seconds convert to milliseconds")]
    [TestCase("47,16", 47160, TestName = "PREVIEWSTART accepts comma decimals")]
    public void PreviewStartConvertsSecondsToMilliseconds(string tagValue, long expectedMs)
    {
        var entry = Scan(chart: BasicChart(extraTags: $"#PREVIEWSTART:{tagValue}"));
        Assert.That(entry.PreviewStartMilliseconds, Is.EqualTo(expectedMs));
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

    // #EDITION is the closest equivalent to FoF's ini "icon" key.
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

        var result = TryScan(chartPath);
        Assert.That(result.HasValue, Is.True, $"Expected UltraStar scan to succeed, but got {result.Error}.");
        return result.Value;
    }

    private ScanExpected<UnpackedIniEntry> TryScan(string chartPath)
        => UnpackedIniEntry.ProcessNewEntry(_songDir, new FileInfo(chartPath), ChartFormat.UltraStar, null, "",
            new FileCollection(new DirectoryInfo(_songDir)));

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
