using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    internal sealed class IniEntryGroup : IEntryGroup
    {
        private static readonly List<UnpackedIniEntry> NO_MATCHES = new();

        private readonly string _directory;
        private readonly HashSet<UnpackedIniEntry> _unpacked = new();
        private readonly Dictionary<string, List<UnpackedIniEntry>> _unpackedByShortname = new();
        private readonly List<SngEntry> _packed = new();

        public string Directory => _directory;

        public IniEntryGroup(string directory)
        {
            _directory = directory;
        }

        public void AddEntry(UnpackedIniEntry entry)
        {
            lock (_unpacked)
            {
                _unpacked.Add(entry);
                if (entry.Shortname != null)
                {
                    if (!_unpackedByShortname.TryGetValue(entry.Shortname, out var matches))
                    {
                        _unpackedByShortname[entry.Shortname] = matches = new List<UnpackedIniEntry>();
                    }
                    matches.Add(entry);
                }
            }
        }

        /// <summary>
        /// Removes all the entries present in this ini group that have a matching shortname.
        /// Indexed by shortname: most shortnames coming out of a songs_updates scan don't
        /// correspond to an already-cached entry, so a miss here is O(1) instead of a full
        /// linear scan of every cached entry in the group.
        /// </summary>
        public List<UnpackedIniEntry> RemoveEntries(string shortname)
        {
            lock (_unpacked)
            {
                if (!_unpackedByShortname.TryGetValue(shortname, out var matches))
                {
                    return NO_MATCHES;
                }

                foreach (var entry in matches)
                {
                    _unpacked.Remove(entry);
                }
                _unpackedByShortname.Remove(shortname);
                return matches;
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

        private void SerializeList<TEntry>(MemoryStream entryStream, ICollection<TEntry> entries, MemoryStream groupStream, Dictionary<SongEntry, CacheWriteIndices> nodes)
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
