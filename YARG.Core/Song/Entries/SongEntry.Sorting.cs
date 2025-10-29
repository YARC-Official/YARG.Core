using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Song.Cache;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    public enum SongAttribute
    {
        Name,
        Artist,
        Album,
        Artist_Album,
        Genre,
        Subgenre,
        Year,
        Charter,
        Playlist,
        Source,
        SongLength,
        DateAdded,
    };

    public static class SongEntrySorting
    {
        public static bool CompareMetadata(SongEntry lhs, SongEntry rhs)
        {
            return MetadataComparer.Instance.Compare(lhs, rhs) < 0;
        }

        public readonly struct MetadataComparer : IComparer<SongEntry>
        {
            public static readonly MetadataComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.Name   .CompareTo(rhs.Name))    == 0 &&
                    (strCmp = lhs.Artist .CompareTo(rhs.Artist))  == 0 &&
                    (strCmp = lhs.Album  .CompareTo(rhs.Album))   == 0 &&
                    (strCmp = lhs.Charter.CompareTo(rhs.Charter)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct ArtistComparer : IComparer<SongEntry>
        {
            public static readonly ArtistComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0 &&
                    (strCmp = lhs.Charter.CompareTo(rhs.Charter)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct AlbumComparer : IComparer<SongEntry>
        {
            public static readonly AlbumComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.AlbumTrack.CompareTo(rhs.AlbumTrack)) == 0 &&
                    (strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0 &&
                    (strCmp = lhs.Charter.CompareTo(rhs.Charter)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct PlaylistComparer : IComparer<SongEntry>
        {
            public static readonly PlaylistComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.PlaylistTrack != rhs.PlaylistTrack)
                {
                    return lhs.PlaylistTrack.CompareTo(rhs.PlaylistTrack);
                }

                if (lhs is RBCONEntry rblhs && rhs is RBCONEntry rbrhs)
                {
                    int lhsBand = rblhs.RBBandDiff;
                    int rhsBand = rbrhs.RBBandDiff;
                    if (lhsBand != rhsBand)
                    {
                        if (lhsBand == -1)
                        {
                            return 1;
                        }
                        if (rhsBand == -1)
                        {
                            return -1;
                        }
                        return lhsBand.CompareTo(rhsBand);
                    }
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct YearComparer : IComparer<SongEntry>
        {
            public static readonly YearComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.YearAsNumber != rhs.YearAsNumber)
                {
                    if (lhs.YearAsNumber == int.MaxValue)
                    {
                        return 1;
                    }
                    if (rhs.YearAsNumber == int.MaxValue)
                    {
                        return -1;
                    }
                    return lhs.YearAsNumber.CompareTo(rhs.YearAsNumber);
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct CharterComparer : IComparer<SongEntry>
        {
            public static readonly CharterComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.AlbumTrack.CompareTo(rhs.AlbumTrack)) == 0 &&
                    (strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct LengthComparer : IComparer<SongEntry>
        {
            public static readonly LengthComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.SongLengthMilliseconds != rhs.SongLengthMilliseconds)
                {
                    return lhs.SongLengthMilliseconds.CompareTo(rhs.SongLengthMilliseconds);
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct InstrumentComparer : IComparer<SongEntry>
        {
            private readonly Instrument _instrument;
            private readonly int _intensity;

            public InstrumentComparer(Instrument instrument, int intensity)
            {
                _instrument = instrument;
                _intensity = intensity;
            }

            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                var otherIntensity = rhs[_instrument].Intensity;
                if (_intensity == otherIntensity)
                {
                    return MetadataComparer.Instance.Compare(lhs, rhs);
                }
                return _intensity != -1 && (otherIntensity == -1 || _intensity < otherIntensity)
                    ? -1 : 1;
            }
        }

        private static readonly unsafe delegate*<SongCache, Dictionary<SongEntry, CacheWriteIndices>, List<string>>[] COLLECTORS =
        {
            &CollectCacheTitles, &CollectCacheArtists,  &CollectCacheAlbums,    &CollectCacheGenres, &CollectCacheSubgenres,
            &CollectCacheYears,  &CollectCacheCharters, &CollectCachePlaylists, &CollectCacheSources,
        };

        internal static void WriteCategoriesToCache(FileStream filestream, SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    nodes.Add(entry, new CacheWriteIndices());
                }
            }

            var categories = new List<string>[CacheReadStrings.NUM_CATEGORIES];
            Parallel.For(0, CacheReadStrings.NUM_CATEGORIES, i =>
            {
                unsafe
                {
                    categories[i] = COLLECTORS[i](cache, nodes);
                }
            });

            using MemoryStream ms = new();
            for (int i = 0; i < categories.Length; ++i)
            {
                var strings = categories[i];
                ms.SetLength(0);
                ms.Write(strings.Count, Endianness.Little);
                foreach (string str in strings)
                {
                    ms.Write(str);
                }

                filestream.Write((int) ms.Length, Endianness.Little);
                filestream.Write(ms.GetBuffer(), 0, (int) ms.Length);
            }
        }

        private static List<string> CollectCacheTitles(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                foreach (var entry in element.Value)
                {
                    var indices = nodes[entry];
                    if (strings.Count == 0 || strings[^1] != entry.Name)
                    {
                        indices.Title = strings.Count;
                        strings.Add(entry.Name);
                    }
                    else
                    {
                        indices.Title = strings.Count - 1;
                    }
                }
            }
            return strings;
        }

        private static List<string> CollectCacheArtists(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Artist != entry.Artist)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Artist);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Artist = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheAlbums(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Album != entry.Album)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Album);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Album = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheGenres(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Genre != entry.Genre)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Genre);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Genre = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheSubgenres(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Subgenre != entry.Subgenre)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Subgenre);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Subgenre = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheYears(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].UnmodifiedYear != entry.UnmodifiedYear)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.UnmodifiedYear);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Year = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheCharters(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Charter != entry.Charter)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Charter);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Charter = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCachePlaylists(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Playlist != entry.Playlist)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Playlist);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Playlist = placements[i];
                }
            }
            return strings;
        }

        private static List<string> CollectCacheSources(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Entries)
            {
                if (element.Value.Count > placements.Length)
                {
                    placements.Resize(element.Value.Count);
                }

                for (int i = 0; i < element.Value.Count; i++)
                {
                    var entry = element.Value[i];
                    var indices = nodes[entry];

                    int query = 0;
                    while (query < i && element.Value[query].Source != entry.Source)
                    {
                        query++;
                    }

                    if (query == i)
                    {
                        placements[i] = strings.Count;
                        strings.Add(entry.Source);
                    }
                    else
                    {
                        placements[i] = placements[query];
                    }
                    indices.Source = placements[i];
                }
            }
            return strings;
        }
    }
}
