using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class IniGroup : ICacheGroup<IniSubEntry>
    {
        public readonly string Directory;
        public readonly Dictionary<HashWrapper, List<IniSubEntry>> entries = new();

        public readonly object iniLock = new();
        private int _count;

        public string Location => Directory;
        public int Count => _count;

        public IniGroup(string directory)
        {
            Directory = directory;
        }

        public void AddEntry(IniSubEntry entry)
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

        public bool TryRemoveEntry(SongEntry entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            if (entries.TryGetValue(entryToRemove.Hash, out var list))
            {
                if (list.RemoveAll(entry => ReferenceEquals(entry, entryToRemove)) > 0)
                {
                    if (list.Count == 0)
                    {
                        entries.Remove(entryToRemove.Hash);
                    }
                    --_count;
                    return true;
                }
            }
            return false;
        }

        public ReadOnlyMemory<byte> SerializeEntries(Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Location);
            writer.Write(_count);
            foreach (var shared in entries)
            {
                foreach (var entry in shared.Value)
                {
                    var buffer = entry.Serialize(nodes[entry], Location);
                    writer.Write(buffer.Length);
                    writer.Write(buffer);
                }
            }
            return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int) ms.Length);
        }
    }
}
