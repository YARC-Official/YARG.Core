using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public interface CategoryConfig<TKey>
    {
        public EntryComparer Comparer { get; }
        public TKey GetKey(SongMetadata entry);
    }

    public readonly struct TitleConfig : CategoryConfig<string>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Name);
        public EntryComparer Comparer => _COMPARER;

        public string GetKey(SongMetadata entry)
        {
            string name = entry.Name.SortStr;
            int i = 0;
            while (i + 1 < name.Length && !char.IsLetterOrDigit(name[i]))
                ++i;

            char character = name[i];
            return char.IsDigit(character) ? "0-9" : char.ToUpperInvariant(character).ToString();
        }
    }

    public readonly struct YearConfig : CategoryConfig<string>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Year);
        public EntryComparer Comparer => _COMPARER;
        public string GetKey(SongMetadata entry)
        {
            return entry.YearAsNumber != int.MaxValue ? entry.Year[..3] + "0s" : entry.Year;
        }
    }

    public readonly struct ArtistAlbumConfig : CategoryConfig<string>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Album);
        public EntryComparer Comparer => _COMPARER;
        public string GetKey(SongMetadata entry)
        {
            return $"{entry.Artist.Str} - {entry.Album.Str}";
        }
    }

    public readonly struct SongLengthConfig : CategoryConfig<string>
    {
        private const int MILLISECONDS_PER_MINUTE = 60 * 1000;
        private static readonly EntryComparer _COMPARER = new(SongAttribute.SongLength);
        public EntryComparer Comparer => _COMPARER;
        public string GetKey(SongMetadata entry)
        {
            return (entry.SongLengthMilliseconds / MILLISECONDS_PER_MINUTE) switch
            {
                < 2 => "00:00 - 02:00",
                < 5 => "02:00 - 05:00",
                < 10 => "05:00 - 10:00",
                < 15 => "10:00 - 15:00",
                < 20 => "15:00 - 20:00",
                _ => "20:00+",
            };
        }
    }

    public readonly struct ArtistConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Artist);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Artist;
    }

    public readonly struct AlbumConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Album);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Album;
    }

    public readonly struct GenreConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Genre);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Genre;
    }

    public readonly struct CharterConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Charter);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Charter;
    }

    public readonly struct PlaylistConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Playlist);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Playlist;
    }

    public readonly struct SourceConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Source);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongMetadata entry) => entry.Source;
    }

    public static class CategorySorter<TKey, TConfig>
        where TConfig : struct, CategoryConfig<TKey>
    {
        private static readonly object  _lock  = new();
        private static readonly TConfig CONFIG = default;

        public static void Add(SongMetadata entry, SortedDictionary<TKey, List<SongMetadata>> sections)
        {
            var key = CONFIG.GetKey(entry);
            lock (_lock)
            {
                if (!sections.TryGetValue(key, out var entries))
                {
                    entries = new();
                    sections.Add(key, entries);
                }
                int index = entries.BinarySearch(entry, CONFIG.Comparer);
                entries.Insert(~index, entry);
            }
        }
    }

    public static class CategoryWriter
    {
        public static void WriteToCache<TKey>(BinaryWriter fileWriter, SortedDictionary<TKey, List<SongMetadata>> sections, SongAttribute attribute, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            List<string> strings = new();
            foreach (var element in sections)
            {
                foreach (var entry in element.Value)
                {
                    string str = entry.GetStringAttribute(attribute);
                    int index = strings.BinarySearch(str);
                    if (index < 0)
                    {
                        index = strings.Count;
                        strings.Add(str);
                    }

                    CategoryCacheWriteNode node;
                    if (attribute == SongAttribute.Name)
                        nodes[entry] = node = new();
                    else
                        node = nodes[entry];

                    switch (attribute)
                    {
                        case SongAttribute.Name: node.title = index; break;
                        case SongAttribute.Artist: node.artist = index; break;
                        case SongAttribute.Album: node.album = index; break;
                        case SongAttribute.Genre: node.genre = index; break;
                        case SongAttribute.Year: node.year = index; break;
                        case SongAttribute.Charter: node.charter = index; break;
                        case SongAttribute.Playlist: node.playlist = index; break;
                        case SongAttribute.Source: node.source = index; break;
                    }
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(strings.Count);
            foreach (string str in strings)
                writer.Write(str);

            fileWriter.Write((int) ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public sealed class InstrumentCategory
    {
        private readonly InstrumentComparer comparer;
        private readonly List<SongMetadata> _entries = new();
        private readonly object entryLock = new();

        public readonly string Key;
        public List<SongMetadata> Entries => _entries;

        public InstrumentCategory(Instrument instrument)
        {
            comparer = new InstrumentComparer(instrument);
            Key = instrument.ToString();
        }

        public void Add(SongMetadata entry)
        {
            if (entry.Parts.HasInstrument(comparer.instrument) ||
                (comparer.instrument == Instrument.Band && entry.Parts.GetValues(Instrument.Band).intensity >= 0))
            {
                lock (entryLock)
                {
                    int index = _entries.BinarySearch(entry, comparer);
                    _entries.Insert(~index, entry);
                }    
            }
        }
    }
}
