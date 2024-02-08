using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.Utility;

namespace YARG.Core.Song.Cache
{
    public abstract class CONGroup : ICacheGroup
    {
        protected readonly Dictionary<string, SortedDictionary<int, SongMetadata>> entries = new();
        protected readonly object entryLock = new();

        private int _count;
        public int Count { get { lock (entryLock) return _count; } }

        public readonly string Location;
        public readonly string DefaultPlaylist;

        protected CONGroup(string location, string defaultPlaylist)
        {
            Location = location;
            DefaultPlaylist = defaultPlaylist;
        }

        public abstract void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings);
        public abstract byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes);

        public void AddEntry(string name, int index, SongMetadata entry)
        {
            lock (entryLock)
            {
                if (entries.TryGetValue(name, out var dict))
                    dict.Add(index, entry);
                else
                    entries.Add(name, new() { { index, entry } });
                ++_count;
            }
        }

        public bool RemoveEntries(string name)
        {
            lock (entryLock)
            {
                if (!entries.Remove(name, out var dict))
                    return false;

                _count -= dict.Count;
            }
            return true;
        }

        public void RemoveEntry(string name, int index)
        {
            lock (entryLock)
            {
                var dict = entries[name];
                dict.Remove(index);
                if (dict.Count == 0)
                {
                    entries.Remove(name);
                }
                --_count;
            }
        }

        public bool TryGetEntry(string name, int index, out SongMetadata? entry)
        {
            entry = null;
            lock (entryLock)
                return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
        }

        public bool TryRemoveEntry(SongMetadata entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            foreach (var dict in entries)
            {
                foreach (var entry in dict.Value)
                {
                    // Intentional compare by reference
                    if (entry.Value == entryToRemove)
                    {
                        dict.Value.Remove(entry.Key);
                        if (dict.Value.Count == 0)
                        {
                            entries.Remove(dict.Key);
                        }
                        --_count;
                        return true;
                    }
                }
            }
            return false;
        }

        protected void Serialize(BinaryWriter writer, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            writer.Write(_count);
            foreach (var entryList in entries)
            {
                foreach (var entry in entryList.Value)
                {
                    writer.Write(entryList.Key);
                    writer.Write(entry.Key);

                    byte[] data = SerializeEntry(entry.Value, nodes[entry.Value]);
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }
        }

        private static byte[] SerializeEntry(SongMetadata entry, CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            using BinaryWriterWrapper writerWrapper = new(writer);

            entry.RBData!.Serialize(writer);
            entry.Serialize(writerWrapper, node);

            return ms.ToArray();
        }
    }
}
