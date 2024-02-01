using System;
using System.Collections.Generic;

namespace YARG.Core.Song.Cache
{
    [Serializable]
    public sealed class SongCache
    {
        private readonly struct DateFlippedComparer : IComparer<DateTime>
        {
            public static readonly DateFlippedComparer COMPARER = default;
            public int Compare(DateTime x, DateTime y)
            {
                return y.CompareTo(x);
            }
        }

        [NonSerialized]
        public readonly Dictionary<HashWrapper, List<SongMetadata>> Entries = new();

        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> ArtistAlbums = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> SongLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongMetadata>> Instruments  = new();
        [NonSerialized]
        public readonly SortedDictionary<DateTime,   List<SongMetadata>> DatesAdded   = new(DateFlippedComparer.COMPARER);

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
