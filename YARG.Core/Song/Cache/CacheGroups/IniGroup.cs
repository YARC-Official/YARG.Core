using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class IniGroup : ICacheGroup<IniSubMetadata>
    {
        public readonly string Directory;
        public readonly Dictionary<HashWrapper, List<IniSubMetadata>> entries = new();

        public readonly object iniLock = new();
        private int _count;

        public string Location => Directory;
        public int Count => _count;

        public IniGroup(string directory)
        {
            Directory = directory;
        }

        public void AddEntry(IniSubMetadata entry)
        {
            var hash = entry.Hash;
            lock (iniLock)
            {
                if (entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    entries.Add(hash, new() { entry });
                ++_count;
            }
        }

        public bool TryRemoveEntry(SongMetadata entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            if (entries.TryGetValue(entryToRemove.Hash, out var list))
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i] == entryToRemove)
                    {
                        list.RemoveAt(i);
                        if (list.Count == 0)
                        {
                            entries.Remove(entryToRemove.Hash);
                        }
                        --_count;
                        return true;
                    }
                }
            }
            return false;
        }

        public byte[] SerializeEntries(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(_count);
            foreach (var shared in entries)
            {
                foreach (var entry in shared.Value)
                {
                    byte[] buffer = SerializeEntry(entry, nodes[entry]);
                    writer.Write(buffer.Length);
                    writer.Write(buffer);
                }
            }
            return ms.ToArray();
        }

        private byte[] SerializeEntry(IniSubMetadata entry, CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            entry.Serialize(writer, Location);
            entry.Serialize(writer, node);

            return ms.ToArray();
        }
    }
}
