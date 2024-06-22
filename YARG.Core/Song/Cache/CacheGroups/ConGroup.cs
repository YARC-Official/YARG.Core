using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public abstract class CONGroup : ICacheGroup<RBCONEntry>
    {
        protected readonly Dictionary<string, SortedDictionary<int, RBCONEntry>> entries = new();

        private int _count;
        public int Count { get { lock (entries) return _count; } }

        public readonly string DefaultPlaylist;

        public abstract string Location { get; }

        protected CONGroup(string defaultPlaylist)
        {
            DefaultPlaylist = defaultPlaylist;
        }

        public abstract void ReadEntry(string nodeName, int index, Dictionary<string, (YARGTextContainer<byte>, RBProUpgrade)> upgrades, UnmanagedMemoryStream stream, CategoryCacheStrings strings);
        public abstract ReadOnlyMemory<byte> SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes);

        public void AddEntry(string name, int index, RBCONEntry entry)
        {
            lock (entries)
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
            lock (entries)
            {
                if (!entries.Remove(name, out var dict))
                    return false;

                _count -= dict.Count;
            }
            return true;
        }

        public void RemoveEntry(string name, int index)
        {
            lock (entries)
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

        public bool TryGetEntry(string name, int index, out RBCONEntry? entry)
        {
            entry = null;
            lock (entries)
            {
                return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
            }
        }

        public bool TryRemoveEntry(SongEntry entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            foreach (var dict in entries)
            {
                foreach (var entry in dict.Value)
                {
                    if (ReferenceEquals(entry.Value, entryToRemove))
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

        protected void Serialize(BinaryWriter writer, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
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

        private static byte[] SerializeEntry(RBCONEntry entry, CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            entry.Serialize(writer, node);
            return ms.ToArray();
        }
    }
}
