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

        private static readonly unsafe delegate*<SongCache, void>[] SORTERS =
        {
            &SortByTitle,    &SortByArtist, &SortByAlbum,       &SortByGenre,  &SortByYear,      &SortByCharter,
            &SortByPlaylist, &SortBySource, &SortByArtistAlbum, &SortByLength, &SortByDateAdded, &SortByInstruments
        };

        internal static unsafe void SortEntries(SongCache cache)
        {
            Parallel.For(0, SORTERS.Length, i => SORTERS[i](cache));
        }

        private static void SortByTitle(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    string name = entry.Name.Group switch
                    {
                        CharacterGroup.Empty or
                        CharacterGroup.AsciiSymbol => "*",
                        CharacterGroup.AsciiNumber => "0-9",
                        _ => char.ToUpper(entry.Name.SortStr[0]).ToString(),
                    };

                    if (!cache.Titles.TryGetValue(name, out var category))
                    {
                        cache.Titles.Add(name, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByArtist(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var artist = entry.Artist;
                    if (!cache.Artists.TryGetValue(artist, out var category))
                    {
                        cache.Artists.Add(artist, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByAlbum(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var album = entry.Album;
                    if (!cache.Albums.TryGetValue(album, out var category))
                    {
                        cache.Albums.Add(album, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, AlbumComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByGenre(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var genre = entry.Genre;
                    if (!cache.Genres.TryGetValue(genre, out var category))
                    {
                        cache.Genres.Add(genre, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByYear(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    string year = entry.YearAsNumber != int.MaxValue ? entry.ParsedYear[..^1] + "0s" : entry.ParsedYear;
                    if (!cache.Years.TryGetValue(year, out var category))
                    {
                        cache.Years.Add(year, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, YearComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByCharter(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var charter = entry.Charter;
                    if (!cache.Charters.TryGetValue(charter, out var category))
                    {
                        cache.Charters.Add(charter, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByPlaylist(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var playlist = entry.Playlist;
                    if (!cache.Playlists.TryGetValue(playlist, out var category))
                    {
                        cache.Playlists.Add(playlist, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, PlaylistComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortBySource(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var source = entry.Source;
                    if (!cache.Sources.TryGetValue(source, out var category))
                    {
                        cache.Sources.Add(source, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByLength(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    // constants represents upper milliseconds limit of each range
                    string range = entry.SongLengthMilliseconds switch
                    {
                        < 120000 => "00:00 - 02:00",
                        < 300000 => "02:00 - 05:00",
                        < 600000 => "05:00 - 10:00",
                        < 900000 => "10:00 - 15:00",
                        < 1200000 => "15:00 - 20:00",
                        _ => "20:00+",
                    };

                    if (!cache.SongLengths.TryGetValue(range, out var category))
                    {
                        cache.SongLengths.Add(range, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, LengthComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByDateAdded(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var dateAdded = entry.GetLastWriteTime().Date;
                    if (!cache.DatesAdded.TryGetValue(dateAdded, out var category))
                    {
                        cache.DatesAdded.Add(dateAdded, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByArtistAlbum(SongCache cache)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var artist = entry.Artist;
                    if (!cache.ArtistAlbums.TryGetValue(artist, out var albums))
                    {
                        cache.ArtistAlbums.Add(artist, albums = new SortedDictionary<SortString, List<SongEntry>>());
                    }

                    var album = entry.Album;
                    if (!albums.TryGetValue(album, out var category))
                    {
                        albums.Add(album, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, AlbumComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByInstruments(SongCache cache)
        {
            Parallel.ForEach(EnumExtensions<Instrument>.Values, instrument =>
            {
                SortedDictionary<int, List<SongEntry>>? intensities = null;
                foreach (var list in cache.Entries)
                {
                    foreach (var entry in list.Value)
                    {
                        var part = entry[instrument];
                        if (part.IsActive())
                        {
                            if (intensities == null)
                            {
                                lock (cache.Instruments)
                                {
                                    cache.Instruments.Add(instrument, intensities = new SortedDictionary<int, List<SongEntry>>());
                                }
                            }

                            if (!intensities.TryGetValue(part.Intensity, out var category))
                            {
                                intensities.Add(part.Intensity, category = new List<SongEntry>());
                            }

                            int index = category.BinarySearch(entry, new InstrumentComparer(instrument, part.Intensity));
                            category.Insert(~index, entry);
                        }
                    }
                }
            });
        }

        private static readonly unsafe delegate*<SongCache, Dictionary<SongEntry, CacheWriteIndices>, List<string>>[] COLLECTORS =
        {
            &CollectCacheTitles, &CollectCacheArtists,  &CollectCacheAlbums,    &CollectCacheGenres,
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
            foreach (var element in cache.Titles)
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
            foreach (var element in cache.Artists)
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
            foreach (var element in cache.Albums)
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
            foreach (var element in cache.Genres)
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

        private static List<string> CollectCacheYears(SongCache cache, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            using var placements = FixedArray<int>.Alloc(64);

            var strings = new List<string>();
            foreach (var element in cache.Years)
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
            foreach (var element in cache.Charters)
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
            foreach (var element in cache.Playlists)
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
            foreach (var element in cache.Sources)
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
