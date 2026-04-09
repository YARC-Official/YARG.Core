using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

public class SongEntrySortingTests
{
    [Test]
    public void CompareMetadata_OrdersByNormalizedName()
    {
        var alpha = CreateEntry(name: "Alpha");
        var beta = CreateEntry(name: "Beta");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SongEntrySorting.CompareMetadata(alpha, beta), Is.True);
            Assert.That(SongEntrySorting.MetadataComparer.Instance.Compare(alpha, beta), Is.LessThan(0));
            Assert.That(SongEntrySorting.MetadataComparer.Instance.Compare(beta, alpha), Is.GreaterThan(0));
        }
    }

    [Test]
    public void MetadataComparer_OrdersByArtistWhenNamesMatch()
    {
        var alphaArtist = CreateEntry(name: "Same Song", artist: "Alpha Artist");
        var betaArtist = CreateEntry(name: "Same Song", artist: "Beta Artist");

        Assert.That(
            SongEntrySorting.MetadataComparer.Instance.Compare(alphaArtist, betaArtist),
            Is.LessThan(0));
    }

    [Test]
    public void MetadataComparer_OrdersByAlbumThenCharterWhenNameAndArtistMatch()
    {
        var firstAlbum = CreateEntry(name: "Same Song", artist: "Same Artist", album: "Album A", charter: "Charter Z");
        var secondAlbum = CreateEntry(name: "Same Song", artist: "Same Artist", album: "Album B", charter: "Charter A");
        var firstCharter = CreateEntry(name: "Same Song", artist: "Same Artist", album: "Same Album", charter: "Alpha");
        var secondCharter = CreateEntry(name: "Same Song", artist: "Same Artist", album: "Same Album", charter: "Beta");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                SongEntrySorting.MetadataComparer.Instance.Compare(firstAlbum, secondAlbum),
                Is.LessThan(0));
            Assert.That(
                SongEntrySorting.MetadataComparer.Instance.Compare(firstCharter, secondCharter),
                Is.LessThan(0));
        }
    }

    [Test]
    public void MetadataComparer_FallsBackToSortBasedLocationWhenNormalizedMetadataMatches()
    {
        var left = CreateEntry(
            name: "  <b>The   Béatles</b>  ",
            artist: "The Ártist",
            album: "Álbum",
            charter: "Čharter",
            sortBasedLocation: "A/song.ini");
        var right = CreateEntry(
            name: "beatles",
            artist: "artist",
            album: "album",
            charter: "charter",
            sortBasedLocation: "B/song.ini");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SongEntrySorting.CompareMetadata(left, right), Is.True);
            Assert.That(
                SongEntrySorting.MetadataComparer.Instance.Compare(left, right),
                Is.LessThan(0));
            Assert.That(
                SongEntrySorting.MetadataComparer.Instance.Compare(right, left),
                Is.GreaterThan(0));
        }
    }

    [Test]
    public void CompareMetadata_ReturnsFalseWhenEntriesCompareEqual()
    {
        var left = CreateEntry(sortBasedLocation: "same/song.ini");
        var right = CreateEntry(sortBasedLocation: "same/song.ini");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SongEntrySorting.MetadataComparer.Instance.Compare(left, right), Is.Zero);
            Assert.That(SongEntrySorting.CompareMetadata(left, right), Is.False);
        }
    }

    private static TestSongEntry CreateEntry(
        string name = "Song Name",
        string artist = "Artist Name",
        string album = "Album Name",
        string charter = "Charter Name",
        string sortBasedLocation = "test-location")
    {
        var entry = new TestSongEntry();
        entry.SetMetadata(name: name, artist: artist, album: album, charter: charter);
        entry.SetLocations(sortBasedLocation);
        return entry;
    }
}
