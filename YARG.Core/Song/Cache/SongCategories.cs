using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YARG.Core.Song.Cache
{
    public abstract class SongCategory<TKey>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        protected readonly object elementLock = new();
        protected readonly SortedDictionary<TKey, List<SongMetadata>> _elements = new();

        public SortedDictionary<TKey, List<SongMetadata>> Elements { get { return _elements; } }

        public abstract void Add(SongMetadata entry);

        protected void Add<TComparer>(TKey key, SongMetadata entry, TComparer comparer)
            where TComparer : IComparer<SongMetadata>
        {
            lock (elementLock)
            {
                if (!_elements.TryGetValue(key, out var node))
                {
                    node = new();
                    _elements.Add(key, node);
                }
                int index = node.BinarySearch(entry, comparer);
                node.Insert(~index, entry);
            }
        }
    }

    [Serializable]
    public abstract class SerializableCategory<TKey> : SongCategory<TKey>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        protected readonly SongAttribute attribute;
        protected readonly EntryComparer comparer;

        public SerializableCategory(SongAttribute attribute)
        {
            switch (attribute)
            {
                case SongAttribute.Unspecified:
                case SongAttribute.SongLength:
                    throw new Exception("stoopid");
            }

            this.attribute = attribute;
            comparer = new(attribute);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            List<string> strings = new();
            foreach (var element in _elements)
            {
                foreach (var entry in element.Value)
                {
                    string str = entry.GetStringAttribute(attribute);
                    int index  = strings.BinarySearch(str);
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

    [Serializable]
    public sealed class NormalCategory : SerializableCategory<SortString>
    {
        public NormalCategory(SongAttribute attribute) : base(attribute)
        {
            Debug.Assert(attribute != SongAttribute.Name && attribute != SongAttribute.Year, "Use the dedicated category for this metadata type");
        }

        public override void Add(SongMetadata entry)
        {
            Add(entry.GetStringAttribute(attribute), entry, comparer);
        }
    }

    [Serializable]
    public sealed class TitleCategory : SerializableCategory<string>
    {
        public TitleCategory() : base(SongAttribute.Name) { }

        public override void Add(SongMetadata entry)
        {
            string name = entry.Name.SortStr;
            int i = 0;
            while (i + 1 < name.Length && !char.IsLetterOrDigit(name[i]))
                ++i;

            char character = name[i];
            if (char.IsDigit(character))
                Add("0-9", entry, comparer);
            else
                Add(char.ToUpper(character).ToString(), entry, comparer);
        }
    }

    [Serializable]
    public sealed class YearCategory : SerializableCategory<string>
    {
        public YearCategory() : base(SongAttribute.Year) { }

        public override void Add(SongMetadata entry)
        {
            if (entry.YearAsNumber != int.MaxValue)
                Add(entry.Year[..3] + "0s", entry, comparer);
            else
                Add(entry.Year, entry, comparer);
        }
    }

    public sealed class ArtistAlbumCategory : SongCategory<string>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.Album);
        public override void Add(SongMetadata entry)
        {
            string key = $"{entry.Artist.Str} - {entry.Album.Str}";
            Add(key, entry, comparer);
        }
    }

    public sealed class SongLengthCategory : SongCategory<string>
    {
        private const int MILLISECONDS_PER_MINUTE = 60 * 1000;
        private static readonly EntryComparer comparer = new(SongAttribute.SongLength);
        public override void Add(SongMetadata entry)
        {
            string key = (entry.SongLengthMilliseconds / MILLISECONDS_PER_MINUTE) switch
            {
                < 2 => "00:00 - 02:00",
                < 5 => "02:00 - 05:00",
                < 10 => "05:00 - 10:00",
                < 15 => "10:00 - 15:00",
                < 20 => "15:00 - 20:00",
                _ => "20:00+",
            };

            Add(key, entry, comparer);
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
