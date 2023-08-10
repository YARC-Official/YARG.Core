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
            if (entry.YearAsNumber != int.MaxValue)
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

    public class InstrumentCategory : SongCategory<SortString>
    {
        private static readonly InstrumentComparer FiveFretGuitarComparer   = new(Instrument.FiveFretGuitar);
        private static readonly InstrumentComparer FiveFretBassComparer     = new(Instrument.FiveFretBass);
        private static readonly InstrumentComparer FiveFretRhythmComparer   = new(Instrument.FiveFretRhythm);
        private static readonly InstrumentComparer FiveFretCoopComparer     = new(Instrument.FiveFretCoopGuitar);
        private static readonly InstrumentComparer SixFretGuitarComparer    = new(Instrument.SixFretGuitar);
        private static readonly InstrumentComparer SixFretBassComparer      = new(Instrument.SixFretBass);
        private static readonly InstrumentComparer SixFretRhythmComparer    = new(Instrument.SixFretRhythm);
        private static readonly InstrumentComparer SixFretCoopComparer      = new(Instrument.SixFretCoopGuitar);
        private static readonly InstrumentComparer KeysComparer             = new(Instrument.Keys);
        private static readonly InstrumentComparer FourLaneDrumComparer     = new(Instrument.FourLaneDrums);
        private static readonly InstrumentComparer ProDrumComparer          = new(Instrument.ProDrums);
        private static readonly InstrumentComparer FiveLaneDrumComparer     = new(Instrument.FiveLaneDrums);
        private static readonly InstrumentComparer VocalsComparer           = new(Instrument.Vocals);
        private static readonly InstrumentComparer HarmonyComparer          = new(Instrument.Harmony);
        private static readonly InstrumentComparer ProGuitar_17FretComparer = new(Instrument.ProGuitar_17Fret);
        private static readonly InstrumentComparer ProGuitar_22FretComparer = new(Instrument.ProGuitar_22Fret);
        private static readonly InstrumentComparer ProBass_17FretComparer   = new(Instrument.ProBass_17Fret);
        private static readonly InstrumentComparer ProBass_22FretComparer   = new(Instrument.ProBass_22Fret);
        private static readonly InstrumentComparer ProKeysComparer          = new(Instrument.ProKeys);

        private static readonly InstrumentComparer[] comparers =
        {
            FiveFretGuitarComparer,
            FiveFretBassComparer,
            FiveFretRhythmComparer,
            FiveFretCoopComparer,
            SixFretGuitarComparer,
            SixFretBassComparer,
            SixFretRhythmComparer,
            SixFretCoopComparer,
            KeysComparer,
            FourLaneDrumComparer,
            ProDrumComparer,
            FiveLaneDrumComparer,
            VocalsComparer,
            HarmonyComparer,
            ProGuitar_17FretComparer,
            ProGuitar_22FretComparer,
            ProBass_17FretComparer,
            ProBass_22FretComparer,
            ProKeysComparer,
        };

        private readonly bool multithreading;
        public InstrumentCategory(bool multithreading)
        {
            this.multithreading = multithreading;
        }

        public override void Add(SongMetadata entry)
        {
            if (multithreading)
            {
                Parallel.ForEach(comparers, comparer =>
                {
                    if (entry.Parts.HasInstrument(comparer.instrument))
                    {
                        Add(comparer.instrument.ToString(), entry, comparer);
                    }
                });
            }
            else
            {
                foreach (var comparer in comparers)
                {
                    if (entry.Parts.HasInstrument(comparer.instrument))
                    {
                        Add(comparer.instrument.ToString(), entry, comparer);
                    }
                }
            }
        }

        public override SortedDictionary<SortString, List<SongMetadata>> GetSongSelectionList()
        {
            return elements;
        }
    }
}
