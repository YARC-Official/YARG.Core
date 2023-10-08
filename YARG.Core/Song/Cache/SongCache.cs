using System;
using System.Collections.Generic;

namespace YARG.Core.Song.Cache
{
    [Serializable]
    public sealed class SongCache
    {
        [NonSerialized]
        public readonly Dictionary<HashWrapper, List<SongMetadata>> Entries = new();

        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> ArtistAlbums = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> SongLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> Instruments  = new();
        public readonly SortedDictionary<string,     List<SongMetadata>> Titles       = new();
        public readonly SortedDictionary<string,     List<SongMetadata>> Years        = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Artists      = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Albums       = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Genres       = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Charters     = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Playlists    = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> Sources      = new();
    }
}
