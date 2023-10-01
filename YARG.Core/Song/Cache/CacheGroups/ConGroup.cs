using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public abstract class CONGroup
    {
        protected readonly Dictionary<string, SortedDictionary<int, SongMetadata>> entries = new();
        protected readonly object entryLock = new();
        private int _entryCount;
        public int EntryCount { get { lock (entryLock) return _entryCount; } }
        public abstract bool ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings);

        public void AddEntry(string name, int index, SongMetadata entry)
        {
            lock (entryLock)
            {
                if (entries.TryGetValue(name, out var dict))
                    dict.Add(index, entry);
                else
                    entries.Add(name, new() { { index, entry } });
                ++_entryCount;
            }
        }

        public bool RemoveEntries(string name)
        {
            lock (entryLock)
            {
                if (!entries.Remove(name, out var dict))
                    return false;

                _entryCount -= dict.Count;
            }
            return true;
        }

        public bool RemoveEntry(string name, int index)
        {
            lock (entryLock)
            {
                if (!entries.TryGetValue(name, out var dict) || !dict.Remove(index))
                    return false;

                --_entryCount;
                if (dict.Count == 0)
                    entries.Remove(name);
            }
            return true;
        }

        public bool TryGetEntry(string name, int index, out SongMetadata? entry)
        {
            entry = null;
            lock (entryLock)
                return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
        }

        protected void Serialize(BinaryWriter writer, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            writer.Write(_entryCount);
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

            entry.RBData!.Serialize(writer);
            entry.Serialize(writer, node);

            return ms.ToArray();
        }
    }
}
