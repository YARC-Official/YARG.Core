using System;
using System.Collections.Generic;

namespace YARG.Core.Song.Cache
{
    [Serializable]
    public class SongCache
    {
        [NonSerialized]
        public readonly Dictionary<HashWrapper, List<SongMetadata>> entries = new();

        public readonly TitleCategory titles = new();
        public readonly YearCategory years = new();

        [NonSerialized]
        public readonly ArtistAlbumCategory artistAlbums = new();
        [NonSerialized]
        public readonly SongLengthCategory songLengths = new();
        [NonSerialized]
        public readonly InstrumentCategory instruments;

        public readonly NormalCategory artists = new(SongAttribute.Artist);
        public readonly NormalCategory albums = new(SongAttribute.Album);
        public readonly NormalCategory genres = new(SongAttribute.Genre);
        public readonly NormalCategory charters = new(SongAttribute.Charter);
        public readonly NormalCategory playlists = new(SongAttribute.Playlist);
        public readonly NormalCategory sources = new(SongAttribute.Source);

        public SongCache(bool multithreading)
        {
            instruments = new(multithreading);
        }
    }
}
