using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public class IniGroup
    {
        public readonly string directory;
        public readonly object iniLock = new();
        public readonly Dictionary<HashWrapper, List<SongMetadata>> entries = new();

        public IniGroup(string directory)
        {
            this.directory = directory;
        }

        public void AddEntry(SongMetadata entry)
        {
            var hash = entry.Hash;
            lock (iniLock)
            {
                if (entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    entries.Add(hash, new() { entry });
            }
        }

        public byte[] Serialize(Dictionary<SongMetadata, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(entries.Count);
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

        private byte[] SerializeEntry(SongMetadata entry, CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Path.GetRelativePath(directory, entry.Directory));
            entry.IniData!.Serialize(writer);
            entry.Serialize(writer, node);

            return ms.ToArray();
        }
    }
}
