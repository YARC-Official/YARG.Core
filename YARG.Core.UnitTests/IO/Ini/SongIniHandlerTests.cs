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

    private static IniModifierCollection ReadSongIni(string iniContents, Encoding? encoding = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, iniContents, encoding ?? Encoding.UTF8);
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
