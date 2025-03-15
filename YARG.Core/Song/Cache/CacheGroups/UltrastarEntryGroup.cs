using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Entries.Ultrastar;

namespace YARG.Core.Song.Cache
{
    internal sealed class UltrastarEntryGroup : IEntryGroup
    {
        private readonly string _directory;
        private readonly List<UltrastarEntry> _entries = new();

        public string Directory => _directory;

        public UltrastarEntryGroup(string directory)
        {
            _directory = directory;
        }

        public void AddEntry(UltrastarEntry entry)
        {
            lock (_entries)
            {
                _entries.Add(entry);
            }
        }

        public void Serialize(MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            groupStream.Write(_directory);
            using MemoryStream entryStream = new();
            SerializeList(entryStream, _entries, groupStream, nodes);
        }

        private void SerializeList<TEntry>(MemoryStream entryStream, List<TEntry> entries, MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> nodes)
            where TEntry : UltrastarEntry
        {
            groupStream.Write(entries.Count, Endianness.Little);
            foreach (var entry in entries)
            {
                entryStream.SetLength(0);

                // Validation block
                string relativePath = Path.GetRelativePath(_directory, entry.ActualLocation);
                if (relativePath == ".")
                {
                    relativePath = string.Empty;
                }
                entryStream.Write(relativePath);

                entry.Serialize(entryStream, nodes[entry]);

                groupStream.Write((int) entryStream.Length, Endianness.Little);
                groupStream.Write(entryStream.GetBuffer(), 0, (int) entryStream.Length);
            }
        }
    }
}
