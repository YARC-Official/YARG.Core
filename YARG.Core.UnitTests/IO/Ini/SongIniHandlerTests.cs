using System.Text;
using NUnit.Framework;
using YARG.Core.IO.Ini;

namespace YARG.Core.UnitTests.IO.Ini;

public class SongIniHandlerTests
{
    [Test]
    public void ReadSongIniFile_ReturnsEmptyCollectionWhenSongSectionIsMissing()
    {
        var modifiers = ReadSongIni(
            """
            [other]
            name = Ignored
            """
        );

        Assert.That(modifiers.IsEmpty(), Is.True);
    }

    [Test]
    public void ReadSongIniFile_UsesCaseInsensitiveSectionAndModifierLookup()
    {
        var modifiers = ReadSongIni(
            """
            [SoNg]
            NAME = Test Name
            ARTIST = Test Artist
            VIDEO_LOOP = 1
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Test Name"));
            Assert.That(modifiers.Extract("artist", out string artist), Is.True);
            Assert.That(artist, Is.EqualTo("Test Artist"));
            Assert.That(modifiers.Extract("video_loop", out bool videoLoop), Is.True);
            Assert.That(videoLoop, Is.True);
        }
    }

    [Test]
    public void ReadSongIniFile_IgnoresUnknownSectionsAndUnknownModifiers()
    {
        var modifiers = ReadSongIni(
            """
            [song]
            name = Kept
            unknown_field = Ignored

            [metadata]
            artist = Also Ignored
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Kept"));
            Assert.That(modifiers.Contains("unknown_field"), Is.False);
            Assert.That(modifiers.Contains("artist"), Is.False);
        }
    }

    [Test]
    public void ReadSongIniFile_AliasOutputsUseLastValueWhenMultipleInputsMapToSameField()
    {
        var modifiers = ReadSongIni(
            """
            [song]
            track = 7
            album_track = 9
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("album_track", out int albumTrack), Is.True);
            Assert.That(albumTrack, Is.EqualTo(9));
            Assert.That(modifiers.IsEmpty(), Is.True);
        }
    }

    [Test]
    public void ReadSongIniFile_ReadsUtf16EncodedFiles()
    {
        var modifiers = ReadSongIni(
            """
            [song]
            name = UTF16 Name
            """,
            Encoding.Unicode
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("UTF16 Name"));
        }
    }

    [TestCase("utf-16BE")]
    [TestCase("utf-32")]
    [TestCase("utf-32BE")]
    public void ReadSongIniFile_DecodesByByteOrderMark(string encodingName)
    {
        var modifiers = ReadSongIni(
            """
            [song]
            name = Café
            """,
            Encoding.GetEncoding(encodingName)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Café"));
        }
    }

    [Test]
    public void ReadSongIniFile_DecodesWholeFileAsIso88591WhenItIsNotValidUtf8()
    {
        // C3 A9 is valid UTF-8 on its own, but the later byte E9 isn't, so the whole file is ISO-8859-1
        var modifiers = ReadSongIni([
            .. "[song]\nname = "u8, 0xC3, 0xA9, .. "\nartist = Caf"u8, 0xE9, .. "\n"u8,
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("\u00C3\u00A9"));
            Assert.That(modifiers.Extract("artist", out string artist), Is.True);
            Assert.That(artist, Is.EqualTo("Café"));
        }
    }

    [Test]
    public void ReadSongIniFile_DecodesInvalidBytesAfterUtf8ByteOrderMarkAsReplacementCharacters()
    {
        var modifiers = ReadSongIni([
            0xEF, 0xBB, 0xBF, .. "[song]\nname = Caf"u8, 0xE9, .. "\n"u8,
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Caf\uFFFD"));
        }
    }

    [Test]
    [SetCulture("tr-TR")]
    public void ReadSongIniFile_MatchesKeysInAnyCaseInAnyCulture()
    {
        var modifiers = ReadSongIni(
            """
            [SONG]
            ARTIST = Someone
            DIFF_GUITAR = 3
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("artist", out string artist), Is.True);
            Assert.That(artist, Is.EqualTo("Someone"));
            Assert.That(modifiers.Extract("diff_guitar", out int guitar), Is.True);
            Assert.That(guitar, Is.EqualTo(3));
        }
    }

    [Test]
    public void ReadSongIniFile_TrimsWhiteSpaceOutsideAsciiFromValues()
    {
        var modifiers = ReadSongIni("[song]\nname = Test\u00A0\nartist = \u00A0Someone\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers.Extract("name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("Test"));
            Assert.That(modifiers.Extract("artist", out string artist), Is.True);
            Assert.That(artist, Is.EqualTo("Someone"));
        }
    }

    private static IniModifierCollection ReadSongIni(string iniContents, Encoding? encoding = null)
    {
        return ReadSongIni(path => File.WriteAllText(path, iniContents, encoding ?? Encoding.UTF8));
    }

    private static IniModifierCollection ReadSongIni(byte[] iniBytes)
    {
        return ReadSongIni(path => File.WriteAllBytes(path, iniBytes));
    }

    private static IniModifierCollection ReadSongIni(Action<string> write)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        try
        {
            write(path);
            return SongIniHandler.ReadSongIniFile(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
