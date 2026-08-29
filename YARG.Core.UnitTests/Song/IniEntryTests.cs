using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.Song;
using YARG.Core.Venue;
using ChartFormat = YARG.Core.Song.ChartFormat;

namespace YARG.Core.UnitTests.Song;

public class IniEntryTests
{
    [TestCase("_clean", true, TestName = "Loads clean video when censoring enabled")]
    [TestCase("_explicit", false, TestName = "Loads explicit video when censoring disabled")]
    public void LoadBackground_LoadsSpecificVideo_BasedOnCensorship(string suffix, bool censoringEnabled)
    {
        const string songName = "testsong";
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, songName);
            var entry = CreateIniEntry(root, songName, CreateBasicIni());

            string videoPath = Path.Combine(path, $"bg{suffix}.mp4");
            File.WriteAllBytes(videoPath, [0x00]);

            using var background = entry.LoadBackground(censoringEnabled);

            Assert.That(background, Is.Not.Null);
            Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestCase("_clean", false, TestName = "Does not load clean video when censoring disabled")]
    [TestCase("_explicit", true, TestName = "Does not load explicit video when censoring enabled")]
    public void LoadBackground_DoesNotLoadSpecificVideo_WhenRejectedByCensorship(string suffix, bool censoringEnabled)
    {
        const string songName = "testsong";
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, songName);
            var entry = CreateIniEntry(root, songName, CreateBasicIni());

            string videoPath = Path.Combine(path, $"bg{suffix}.mp4");
            File.WriteAllBytes(videoPath, [0x00]);

            using var background = entry.LoadBackground(censoringEnabled);

            Assert.That(background, Is.Null);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestCase("_explicit", true, TestName = "Falls back to normal video when explicit is rejected")]
    [TestCase("_clean", false, TestName = "Falls back to normal video when clean is rejected")]
    public void LoadBackground_FallsBackToNormalBackground_WhenSpecificVideoRejected(string rejectedSuffix,
        bool censoringEnabled)
    {
        const string songName = "testsong";
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, songName);
            var entry = CreateIniEntry(root, songName, CreateBasicIni());

            string specificVideoPath = Path.Combine(path, $"bg{rejectedSuffix}.mp4");
            File.WriteAllBytes(specificVideoPath, [0x00]);

            string videoPath = Path.Combine(path, "bg.mp4");
            File.WriteAllBytes(videoPath, [0x01]);

            using var background = entry.LoadBackground(censoringEnabled);

            Assert.That(background, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
                Assert.That(background.Stream!.ReadByte(), Is.EqualTo(0x01));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestCase(true, 0x02, TestName = "Prioritizes clean video over base and explicit videos when censoring enabled")]
    [TestCase(false, 0x03, TestName = "Prioritizes explicit video over base and clean videos when censoring disabled")]
    public void LoadBackground_PrioritizesCorrectVideo_WhenMultipleFilesExist(bool censoringEnabled, int expectedByte)
    {
        const string songName = "testsong";
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, songName);
            var entry = CreateIniEntry(root, songName, CreateBasicIni());

            File.WriteAllBytes(Path.Combine(path, "bg.mp4"), [0x01]);
            File.WriteAllBytes(Path.Combine(path, "bg_clean.mp4"), [0x02]);
            File.WriteAllBytes(Path.Combine(path, "bg_explicit.mp4"), [0x03]);

            using var background = entry.LoadBackground(censoringEnabled);

            Assert.That(background, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
                Assert.That(background.Stream!.ReadByte(), Is.EqualTo(expectedByte));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void LoadBackground_UsesFixedVideoNamesInsideSongDirectory()
    {
        string root = CreateTempDirectory();
        try
        {
            var entry = CreateIniEntry(
                root,
                "testsong",
                CreateBasicIni()
            );

            string videoPath = Path.Combine(root, "testsong", "bg.mp4");
            File.WriteAllBytes(videoPath, [0x00]);

            using var background = entry.LoadBackground(false);

            Assert.That(background, Is.Not.Null);
            Assert.That(background!.Type, Is.EqualTo(BackgroundType.Video));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static UnpackedIniEntry CreateIniEntry(string root, string name, string iniText)
    {
        string songDirectory = Path.Combine(root, name);
        Directory.CreateDirectory(songDirectory);

        string midiPath = Path.Combine(songDirectory, "notes.mid");
        File.Copy(GetTestMidiPath(), midiPath);

        string audioPath = Path.Combine(songDirectory, "song.opus");
        using (var opus = new FileStream(audioPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            opus.Write(0x00, Endianness.Little);
        }

        string iniPath = Path.Combine(songDirectory, "song.ini");
        File.WriteAllText(iniPath, iniText);

        // TODO: We should probably be creating a test entry, not an actual entry, because it really doesn't like doing certain things in a test environment. But for now, this works.
        var result = UnpackedIniEntry.ProcessNewEntry(songDirectory, new FileInfo(midiPath), ChartFormat.Mid,
            new FileInfo(iniPath), null, "");
        Assert.That(result.HasValue, Is.True, $"Expected ini creation to succeed, but got {result.Error}.");
        return result.Value;
    }

    private static string CreateBasicIni()
    {
        return """
               [song]
               artist = ARTIST
               name = NAME
               album = ALBUM
               year = 1234
               genre = GENRE
               subgenre = SUBGENRE
               pro_drums = True
               diff_drums = 0
               diff_drums_real = 1
               diff_bass = 2
               diff_guitar = 3
               diff_keys = 4
               diff_keys_real = 5
               diff_vocals = 6
               diff_vocals_harm = 7
               diff_band = 8
               preview_start_time = 16000
               song_length = 128000
               icon = yarg
               charter = CHARTER
               loading_phrase = LOADING PHRASE
               """;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"yarg-ini-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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