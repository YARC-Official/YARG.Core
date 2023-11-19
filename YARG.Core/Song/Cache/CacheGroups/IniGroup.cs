using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class IniGroup : ICacheGroup
    {
        public readonly object iniLock = new();
        public readonly Dictionary<HashWrapper, List<SongMetadata>> entries = new();
        private int _count;

        public void AddEntry(SongMetadata entry)
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

        public byte[] SerializeEntries(string directory, Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(_count);
            foreach (var shared in entries)
            {
                foreach (var entry in shared.Value)
                {
                    byte[] buffer = SerializeEntry(directory, entry, nodes[entry]);
                    writer.Write(buffer.Length);
                    writer.Write(buffer);
                }
            }
            return ms.ToArray();
        }

        private byte[] SerializeEntry(string directory, SongMetadata entry, CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            entry.IniData!.Serialize(writer, directory);
            entry.Serialize(writer, node);

            return ms.ToArray();
        }
    }
}
