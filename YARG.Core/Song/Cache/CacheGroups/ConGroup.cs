using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public abstract class CONGroup
    {
        protected readonly Dictionary<string, SortedDictionary<int, SongMetadata>> entries = new();
        protected readonly object entryLock = new();
        public int EntryCount
        {
            get
            {
                int count = 0;
                foreach (var node in entries)
                    count += node.Value.Count;
                return count;
            }
        }
        public abstract void ReadEntry(string nodeName, int index, Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades, YARGBinaryReader reader, CategoryCacheStrings strings);

        public void AddEntry(string name, int index, SongMetadata entry)
        {
            lock (entryLock)
            {
                if (entries.TryGetValue(name, out var dict))
                    dict.Add(index, entry);
                else
                    entries.Add(name, new() { { index, entry } });
            }
        }

        public void RemoveEntries(string name) { lock (entryLock) entries.Remove(name); }

        public void RemoveEntry(string name, int index) { lock (entryLock) entries[name].Remove(index); }

        public bool TryGetEntry(string name, int index, out SongMetadata? entry)
        {
            entry = null;
            return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
        }

        protected void Serialize(BinaryWriter writer, ref Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            writer.Write(EntryCount);
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
