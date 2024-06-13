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
        public readonly Dictionary<HashWrapper, List<SongEntry>> Entries = new();

        public readonly SortedDictionary<string,     SortedSet<SongEntry>> Titles       = new();
        public readonly SortedDictionary<string,     SortedSet<SongEntry>> Years        = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Artists      = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Albums       = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Genres       = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Charters     = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Playlists    = new();
        public readonly SortedDictionary<SortString, SortedSet<SongEntry>> Sources      = new();

        [NonSerialized]
        public readonly SortedDictionary<string,   SortedSet<SongEntry>> ArtistAlbums = new(StringComparer.InvariantCultureIgnoreCase);
        [NonSerialized]
        public readonly SortedDictionary<string,   SortedSet<SongEntry>> SongLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<DateTime, SortedSet<SongEntry>> DatesAdded   = new(DateFlippedComparer.COMPARER);

        [NonSerialized]
        public readonly SortedDictionary<Instrument, SortedDictionary<int, SortedSet<SongEntry>>> Instruments = new();

        public SongCache()
        {
            foreach (var ins in (Instrument[])Enum.GetValues(typeof(Instrument)))
            {
                Instruments.Add(ins, new SortedDictionary<int, SortedSet<SongEntry>>());
            }
        }
    }
}
