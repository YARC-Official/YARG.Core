using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public abstract class SongCategory<TKey>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        protected readonly object elementLock = new();
        protected readonly SortedDictionary<TKey, List<SongMetadata>> elements = new();

        public abstract void Add(SongMetadata entry);

        protected void Add(TKey key, SongMetadata entry, EntryComparer comparer)
        {
            lock (elementLock)
            {
                if (!elements.TryGetValue(key, out var node))
                {
                    node = new();
                    elements.Add(key, node);
                }
                int index = node.BinarySearch(entry, comparer);
                node.Insert(~index, entry);
            }
        }

        public SortedDictionary<TKey, List<SongMetadata>>.Enumerator GetEnumerator() { return elements.GetEnumerator(); }

        public abstract SortedDictionary<TKey, List<SongMetadata>> GetSongSelectionList();
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
            int index = -1;
            foreach (var element in this)
            {
                foreach (var entry in element.Value)
                {
                    string str = entry.GetStringAttribute(attribute);
                    if (index == -1 || strings[index] != str)
                    {
                        strings.Add(str);
                        index++;
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
            writer.Write(index + 1);
            foreach (string str in strings)
                writer.Write(str);

            fileWriter.Write((int) ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    [Serializable]
    public class NormalCategory : SerializableCategory<SortString>
    {
        public NormalCategory(SongAttribute attribute) : base(attribute)
        {
            Debug.Assert(attribute != SongAttribute.Name && attribute != SongAttribute.Year, "Use the dedicated category for this metadata type");
        }

        public override void Add(SongMetadata entry)
        {
            Add(entry.GetStringAttribute(attribute), entry, comparer);
        }

        public override SortedDictionary<SortString, List<SongMetadata>> GetSongSelectionList()
        {
            return elements;
        }
    }

    [Serializable]
    public class TitleCategory : SerializableCategory<string>
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

        public override SortedDictionary<string, List<SongMetadata>> GetSongSelectionList()
        {
            SortedDictionary<string, List<SongMetadata>> map = new();
            foreach (var element in this)
                map.Add(element.Key, element.Value);
            return map;
        }
    }

    [Serializable]
    public class YearCategory : SerializableCategory<string>
    {
        public YearCategory() : base(SongAttribute.Year) { }

        public override void Add(SongMetadata entry)
        {
            if (entry.YearAsNumber != 0)
                Add(entry.Year[..3] + "0s", entry, comparer);
            else
                Add(entry.Year, entry, comparer);
        }

        public override SortedDictionary<string, List<SongMetadata>> GetSongSelectionList()
        {
            SortedDictionary<string, List<SongMetadata>> map = new();
            foreach (var element in this)
                map.Add(element.Key, element.Value);
            return map;
        }
    }

    public class ArtistAlbumCategory : SongCategory<string>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.Album);
        public override void Add(SongMetadata entry)
        {
            string key = $"{entry.Artist.Str} - {entry.Album.Str}";
            Add(key, entry, comparer);
        }

        public override SortedDictionary<string, List<SongMetadata>> GetSongSelectionList()
        {
            return elements;
        }
    }

    public class SongLengthCategory : SongCategory<string>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.SongLength);
        public override void Add(SongMetadata entry)
        {
            string key = TimeSpan.FromMilliseconds(entry.SongLength).TotalMinutes switch
            {
                <= 0.00 => "-",
                <= 2.00 => "00:00 - 02:00",
                <= 5.00 => "02:00 - 05:00",
                <= 10.00 => "05:00 - 10:00",
                <= 15.00 => "10:00 - 15:00",
                <= 20.00 => "15:00 - 20:00",
                _ => "20:00+",
            };

            Add(key, entry, comparer);
        }

        public override SortedDictionary<string, List<SongMetadata>> GetSongSelectionList()
        {
            return elements;
        }
    }
}
