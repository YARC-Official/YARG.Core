﻿using System;
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

        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongEntry>> ArtistAlbums = new(StringComparer.InvariantCultureIgnoreCase);
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongEntry>> SongLengths  = new();
        [NonSerialized]
        public readonly SortedDictionary<string,     List<SongEntry>> Instruments  = new();
        [NonSerialized]
        public readonly SortedDictionary<DateTime,   List<SongEntry>> DatesAdded   = new(DateFlippedComparer.COMPARER);

        public readonly SortedDictionary<string,     List<SongEntry>> Titles       = new();
        public readonly SortedDictionary<string,     List<SongEntry>> Years        = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Artists      = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Albums       = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Genres       = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Charters     = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Playlists    = new();
        public readonly SortedDictionary<SortString, List<SongEntry>> Sources      = new();
    }
}
