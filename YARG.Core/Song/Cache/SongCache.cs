using System;
using System.Collections.Generic;

namespace YARG.Core.Song
{
    [Serializable]
    public sealed class SongCache
    {
        private readonly struct DateFlippedComparer : IComparer<DateTime>
        {
            public static readonly DateFlippedComparer Instance = default;
            public int Compare(DateTime x, DateTime y)
            {
                return y.CompareTo(x);
            }
        }

        [NonSerialized]
        public readonly Dictionary<HashWrapper, List<SongEntry>> Entries = new();

        public readonly SortedDictionary<string,     List<SongEntry>> Titles       = new();
        public readonly SortedDictionary<string,     List<SongEntry>> Years        = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Artists      = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Albums       = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Genres       = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Subgenres    = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Charters     = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Playlists    = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Sources      = new();

        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongEntry>> SongLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<DateTime,   List<SongEntry>> DatesAdded   = new(DateFlippedComparer.Instance);
        [NonSerialized]
        public readonly SortedDictionary<SortString, SortedDictionary<SortString, List<SongEntry>>> ArtistAlbums = new();

        [NonSerialized]
        public readonly SortedDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> Instruments = new();
    }
}
