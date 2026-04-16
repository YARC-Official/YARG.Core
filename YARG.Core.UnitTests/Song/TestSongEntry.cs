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
        string? year = null,
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
        if (year != null)
        {
            _metadata.Year = year;
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

    public void SetTimingMetadata(
        long? songLength = null,
        long? songOffset = null,
        (long Start, long End)? preview = null,
        (long Start, long End)? video = null)
    {
        if (songLength.HasValue)
        {
            _metadata.SongLength = songLength.Value;
        }
        if (songOffset.HasValue)
        {
            _metadata.SongOffset = songOffset.Value;
        }
        if (preview.HasValue)
        {
            _metadata.Preview = preview.Value;
        }
        if (video.HasValue)
        {
            _metadata.Video = video.Value;
        }
    }

    public void SetVocalMetadata(float? vocalScrollSpeedScalingFactor = null, VocalGender? vocalGender = null)
    {
        _metadata.VocalScrollSpeedScalingFactor = vocalScrollSpeedScalingFactor;
        if (vocalGender.HasValue)
        {
            _metadata.VocalGender = vocalGender.Value;
        }
    }

    public static AvailableParts FinalizeDrumsForTest(AvailableParts parts, DrumsType drumsType)
    {
        FinalizeDrums(ref parts, drumsType);
        return parts;
    }

    public static bool IsValidForTest(in AvailableParts parts)
    {
        return IsValid(in parts);
    }

    public override DateTime GetLastWriteTime() => DateTime.UnixEpoch;

    public override SongChart? LoadChart() => null;

    public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems) => null;

    public override StemMixer? LoadPreviewAudio(float speed) => null;

    public override YARGImage? LoadAlbumData() => null;

    public override BackgroundResult? LoadBackground() => null;

    public override FixedArray<byte>? LoadMiloData() => null;
}
