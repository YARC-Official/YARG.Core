using NUnit.Framework;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.UnitTests.Audio;

namespace YARG.Core.UnitTests.Song;

public class SingStarIniEntryTests
{
    private string _root = null!;
    private string _songDir = null!;

    [SetUp]
    public void SetUp()
    {
        // ScanSingStar falls back to audio-duration lookup for SongLength, which
        // otherwise throws if no audio backend has ever been initialized.
        GlobalAudioHandler.Initialize<NullAudioManager>();

        _root = Path.Combine(Path.GetTempPath(), $"yarg-ss-{Guid.NewGuid():N}");
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
    public void DiscoversMelodyXmlWithoutSongIni()
    {
        // SingStar folders commonly ship without a song.ini, so a fixed-name XML chart
        // has to be enough on its own.
        WriteChart(_songDir, "melody.xml", Chart(title: "Melody Only"));
        WriteAudio(_songDir, "song.mp3");

        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "Melody Only" }));
    }

    [Test]
    public void DiscoversNotesXmlWithoutSongIni()
    {
        WriteChart(_songDir, "notes.xml", Chart(title: "Notes Xml"));
        WriteAudio(_songDir, "song.mp3");

        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "Notes Xml" }));
    }

    [Test]
    public void MalformedParentChartStopsTraversalIntoNestedChild()
    {
        // Discovering a chart candidate must stop traversal even when that chart fails
        // to parse; otherwise the child folder's chart gets indexed as its own song.
        WriteChart(_songDir, "melody.xml", "<MELODY Version=\"1\"><SENTENCE><NOTE");
        WriteAudio(_songDir, "song.mp3");

        string childDir = Path.Combine(_songDir, "nested-child");
        Directory.CreateDirectory(childDir);
        WriteChart(childDir, "melody.xml", Chart(title: "Nested Child"));
        WriteAudio(childDir, "song.mp3");

        string badSongsPath = Path.Combine(_root, "badsongs.txt");
        Assert.That(ScanFolderForNames(badSongsPath), Is.Empty);
        Assert.That(File.ReadAllText(badSongsPath), Does.Contain("Corruption"));
    }

    [Test]
    public void FoldersWithSongIniStillScanTheirFixedNameCharts()
    {
        File.WriteAllText(Path.Combine(_songDir, "song.ini"), "[song]\nname = Ini Song\n");
        WriteChart(_songDir, "melody.xml", Chart(title: "Xml Title"));
        WriteAudio(_songDir, "song.mp3");

        // song.ini wins for metadata, but the XML chart still provides the chart data.
        Assert.That(ScanFolderForNames(), Is.EqualTo(new[] { "Ini Song" }));
    }

    /// <summary>Runs a full library scan over the song folder and returns the entry names.</summary>
    private string[] ScanFolderForNames(string? badSongsPath = null)
        => RunFullScan(badSongsPath).Entries.Values
            .SelectMany(list => list)
            .Select(entry => entry.Name.Original)
            .OrderBy(name => name)
            .ToArray();

    private SongCache RunFullScan(string? badSongsPath = null)
        => CacheHandler.RunScan(false,
            Path.Combine(_root, "songcache.bin"),
            badSongsPath ?? Path.Combine(_root, "badsongs.txt"),
            false,
            new List<string> { _songDir });

    private static void WriteChart(string directory, string fileName, string content)
        => File.WriteAllText(Path.Combine(directory, fileName), content);

    private static void WriteAudio(string directory, string fileName)
        => File.WriteAllBytes(Path.Combine(directory, fileName), new byte[] { 0x00 });

    private static string Chart(string title = "Test Song", string artist = "Test Artist")
        => "<MELODY Version=\"1\" Tempo=\"120\">\n" +
           $"<!-- Artist: {artist} -->\n" +
           $"<!-- Title: {title} -->\n" +
           "<SENTENCE>\n" +
           "<NOTE MidiNote=\"60\" Duration=\"8\" Lyric=\"Hello\"/>\n" +
           "</SENTENCE>\n" +
           "</MELODY>\n";
}
