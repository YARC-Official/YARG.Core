using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    internal sealed class IniEntryGroup : IEntryGroup
    {
        private readonly string _directory;
        private readonly List<UnpackedIniEntry> _unpacked = new();
        private readonly List<SngEntry> _packed = new();

        public string Directory => _directory;

        public IniEntryGroup(string directory)
        {
            _directory = directory;
        }

        private readonly Dictionary<string, UnpackedIniEntry> _byShortname = new();

        public void AddEntry(UnpackedIniEntry entry)
        {
            lock (_unpacked)
            {
                _unpacked.Add(entry);
                if (entry.Shortname != null)
                {
                    _byShortname[entry.Shortname] = entry;
                }
            }
        }

        public void RemoveEntries(string shortname)
        {
            lock (_unpacked)
            {
                if (_byShortname.Remove(shortname, out var entry))
                {
                    _unpacked.Remove(entry);
                }
            }
        }

        public void AddEntry(SngEntry entry)
        {
            lock (_packed)
            {
                _packed.Add(entry);
            }
        }

        public void Serialize(MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> nodes)
        {
            groupStream.Write(_directory);
            using MemoryStream entryStream = new();
            SerializeList(entryStream, _unpacked, groupStream, nodes);
            SerializeList(entryStream, _packed, groupStream, nodes);
        }

        private void SerializeList<TEntry>(MemoryStream entryStream, List<TEntry> entries, MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> nodes)
            where TEntry : IniSubEntry
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
