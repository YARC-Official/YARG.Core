using System;
using System.Collections.Generic;

namespace YARG.Core.Song.Cache
{
    [Serializable]
    public sealed class SongCache
    {
        [NonSerialized]
        public readonly Dictionary<HashWrapper, List<SongMetadata>> entries = new();

        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> artistAlbums = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> songLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> instruments  = new();
        public readonly SortedDictionary<string,     List<SongMetadata>> titles       = new();
        public readonly SortedDictionary<string,     List<SongMetadata>> years        = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> artists      = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> albums       = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> genres       = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> charters     = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> playlists    = new();
        public readonly SortedDictionary<SortString, List<SongMetadata>> sources      = new();
    }
}
