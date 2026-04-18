using System.Text;
using NUnit.Framework;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongMetadataTests
{
    [Test]
    public void CreateFromIni_MapsBasicMetadataFromSongIni()
    {
        var metadata = CreateMetadataFromSongIni(
            """
            [song]
            name = Test Name
            artist = Test Artist
            covered_by = Test Cover Artist
            album = Test Album
            genre = Rock
            sub_genre = Alt Rock
            icon = Custom Source
            playlist = Featured
            loading_phrase = Ready to play
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.Name, Is.EqualTo("Test Name"));
            Assert.That(metadata.Artist, Is.EqualTo("Test Artist"));
            Assert.That(metadata.CoveredBy, Is.EqualTo("Test Cover Artist"));
            Assert.That(metadata.Album, Is.EqualTo("Test Album"));
            Assert.That(metadata.Genre, Is.EqualTo("Rock"));
            Assert.That(metadata.Subgenre, Is.EqualTo("Alt Rock"));
            Assert.That(metadata.Source, Is.EqualTo("Custom Source"));
            Assert.That(metadata.Playlist, Is.EqualTo("Featured"));
            Assert.That(metadata.LoadingPhrase, Is.EqualTo("Ready to play"));
        }
    }

    [Test]
    public void CreateFromIni_UsesAliasesAndDefaultTrackFallbacksFromSongIni()
    {
        var metadata = CreateMetadataFromSongIni(
            """
            [song]
            frets = Alias Charter
            track = 7
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.Charter, Is.EqualTo("Alias Charter"));
            Assert.That(metadata.AlbumTrack, Is.EqualTo(7));
            Assert.That(metadata.PlaylistTrack, Is.EqualTo(int.MaxValue));
        }
    }

    [Test]
    public void FillFromIni_DoesNotOverwriteExistingTextWithEmptyStringValues()
    {
        var metadata = SongMetadata.Default;
        metadata.Name = "Existing Name";
        metadata.Artist = "Existing Artist";
        metadata.Album = "Existing Album";

        var modifiers = ReadSongIni(
            """
            [song]
            name =
            artist =
            album =
            """
        );

        SongMetadata.FillFromIni(ref metadata, modifiers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.Name, Is.EqualTo("Existing Name"));
            Assert.That(metadata.Artist, Is.EqualTo("Existing Artist"));
            Assert.That(metadata.Album, Is.EqualTo("Existing Album"));
        }
    }

    [TestCase(", 1999", "1999")]
    [TestCase(",1999", "1999")]
    [TestCase("1999", "1999")]
    public void FillFromIni_UsesYearChartWhenYearIsMissing(string yearChart, string expectedYear)
    {
        var modifiers = CreateModifiers(
            ("year_chart", yearChart, ModifierType.String)
        );

        var metadata = SongMetadata.CreateFromIni(modifiers);

        Assert.That(metadata.Year, Is.EqualTo(expectedYear));
    }

    [Test]
    public void CreateFromIni_UsesPreviewRangeFromSongIni()
    {
        var metadata = CreateMetadataFromSongIni(
            """
            [song]
            preview = 1000 2500
            """
        );

        Assert.That(metadata.Preview, Is.EqualTo((1000L, 2500L)));
    }

    [Test]
    public void FillFromIni_UsesPreviewSecondsFallbackWhenDirectPreviewIsMissing()
    {
        var modifiers = CreateModifiers(
            ("preview_start_seconds", "1.25", ModifierType.Double),
            ("preview_end_seconds", "2.5", ModifierType.Double)
        );

        var metadata = SongMetadata.CreateFromIni(modifiers);

        Assert.That(metadata.Preview, Is.EqualTo((1250L, 2500L)));
    }

    [Test]
    public void FillFromIni_PrefersNonZeroDelayAndFallsBackToDelaySecondsWhenDelayIsZero()
    {
        var explicitDelay = SongMetadata.CreateFromIni(CreateModifiers(
            ("delay", "500", ModifierType.Int64),
            ("delay_seconds", "3.5", ModifierType.Double)
        ));

        var fallbackDelay = SongMetadata.CreateFromIni(CreateModifiers(
            ("delay", "0", ModifierType.Int64),
            ("delay_seconds", "3.5", ModifierType.Double)
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(explicitDelay.SongOffset, Is.EqualTo(500));
            Assert.That(fallbackDelay.SongOffset, Is.EqualTo(3500));
        }
    }

    [Test]
    public void Default_HasUnspecifiedSongRating()
    {
        Assert.That(SongMetadata.Default.SongRating, Is.EqualTo(SongRating.Unspecified));
    }

    [Test]
    public void CreateFromIni_LeavesSongRatingUnspecifiedWhenRatingIsMissing()
    {
        var metadata = CreateMetadataFromSongIni(
            """
            [song]
            name = No Rating Song
            """
        );

        Assert.That(metadata.SongRating, Is.EqualTo(SongRating.Unspecified));
    }

    [TestCase("0", SongRating.Unspecified)]
    [TestCase("1", SongRating.Family_Friendly)]
    [TestCase("2", SongRating.Supervision_Recommended)]
    [TestCase("3", SongRating.Mature)]
    [TestCase("4", SongRating.No_Rating)]
    [TestCase("5", SongRating.Sensitive_Content)]
    [TestCase("999", SongRating.Unspecified)]
    public void CreateFromIni_NormalizesSongRatingValues(string rawRating, SongRating expectedRating)
    {
        var metadata = SongMetadata.CreateFromIni(CreateModifiers(
            ("rating", rawRating, ModifierType.UInt32)
        ));

        Assert.That(metadata.SongRating, Is.EqualTo(expectedRating));
    }

    [Test]
    public void CreateFromIni_MapsFlagsAndNumericConversionsFromSongIni()
    {
        var metadata = CreateMetadataFromSongIni(
            """
            [song]
            tags = cover
            video_loop = 1
            vocal_scroll_speed = 80
            rating = 3
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.IsMaster, Is.False);
            Assert.That(metadata.VideoLoop, Is.True);
            Assert.That(metadata.VocalScrollSpeedScalingFactor, Is.EqualTo(0.8f));
            Assert.That(metadata.SongRating, Is.EqualTo(SongRating.Mature));
        }
    }

    private static SongMetadata CreateMetadataFromSongIni(string iniContents)
    {
        var modifiers = ReadSongIni(iniContents);
        return SongMetadata.CreateFromIni(modifiers);
    }

    private static IniModifierCollection ReadSongIni(string iniContents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, iniContents);
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

    private static IniModifierCollection CreateModifiers(params (string Output, string RawValue, ModifierType Type)[] entries)
    {
        var collection = new IniModifierCollection();
        foreach (var (output, rawValue, type) in entries)
        {
            AddModifier(collection, output, rawValue, type);
        }
        return collection;
    }

    private static void AddModifier(IniModifierCollection collection, string output, string rawValue, ModifierType type)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawValue);
        using var buffer = FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var container = new YARGTextContainer<byte>(buffer, YARGTextReader.UTF8Strict);
        YARGTextReader.SkipPureWhitespace(ref container);

        var outline = new IniModifierOutline(output, type);
        collection.Add(ref container, in outline, false);
    }
}
