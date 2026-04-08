using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song;

internal sealed class TestSongEntry : SongEntry
{
    private string _sortBasedLocation = "test-location";
    private string _actualLocation = "test-location";

    public override EntryType SubType => EntryType.Ini;

    public override string SortBasedLocation => _sortBasedLocation;

    public override string ActualLocation => _actualLocation;

    public void SetParts(AvailableParts parts)
    {
        _parts = parts;
    }

    public void SetMetadata(
        string? name = null,
        string? artist = null,
        string? album = null,
        string? charter = null,
        string? genre = null,
        string? subgenre = null,
        string? source = null,
        string? playlist = null)
    {
        _metadata = SongMetadata.Default;
        if (name != null)
        {
            _metadata.Name = name;
        }
        if (artist != null)
        {
            _metadata.Artist = artist;
        }
        if (album != null)
        {
            _metadata.Album = album;
        }
        if (charter != null)
        {
            _metadata.Charter = charter;
        }
        if (genre != null)
        {
            _metadata.Genre = genre;
        }
        if (subgenre != null)
        {
            _metadata.Subgenre = subgenre;
        }
        if (source != null)
        {
            _metadata.Source = source;
        }
        if (playlist != null)
        {
            _metadata.Playlist = playlist;
        }

        SetSortStrings();
    }

    public void SetLocations(string sortBasedLocation, string? actualLocation = null)
    {
        _sortBasedLocation = sortBasedLocation;
        _actualLocation = actualLocation ?? sortBasedLocation;
    }

    public override DateTime GetLastWriteTime() => DateTime.UnixEpoch;

    public override SongChart? LoadChart() => null;

    public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems) => null;

    public override StemMixer? LoadPreviewAudio(float speed) => null;

    public override YARGImage? LoadAlbumData() => null;

    public override BackgroundResult? LoadBackground() => null;

    public override FixedArray<byte>? LoadMiloData() => null;
}
