using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    internal sealed class SequentialCacheHandler : CacheHandler
    {
        public SequentialCacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
            : base(baseDirectories, allowDuplicates, fullDirectoryPlaylists) { }

        protected override void FindNewEntries(PlaylistTracker tracker)
        {
            foreach (var group in iniGroups)
            {
                var dirInfo = new DirectoryInfo(group.Directory);
                ScanDirectory(dirInfo, group, tracker);
            }

            foreach (var (_, list) in updates)
            {
                // Orders the updates from oldest to newest to apply more recent information last
                list.Sort();
            }

            foreach (var group in conGroups)
            {
                var reader = group.LoadSongs();
                if (reader != null)
                {
                    try
                    {
                        TraverseCONGroup(reader, (string name, int index) => ScanPackedCONNode(group, name, index, reader));
                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error while scanning packed CON group {group.Location}!");
                    }
                }
                group.CONFile.Dispose();
            }

            foreach (var group in extractedConGroups)
            {
                var reader = group.LoadDTA();
                if (reader != null)
                {
                    try
                    {
                        TraverseCONGroup(reader, (string name, int index) => ScanUnpackedCONNode(group, name, index, reader));
                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error while scanning unpacked CON group {group.Location}!");
                    }
                }
            }
        }

        protected override bool AddEntry(SongEntry entry)
        {
            var hash = entry.Hash;
            if (cache.Entries.TryGetValue(hash, out var list) && !allowDuplicates)
            {
                if (list[0].IsPreferedOver(entry))
                {
                    duplicatesRejected.Add(entry);
                    return false;
                }

                duplicatesToRemove.Add(list[0]);
                list[0] = entry;
            }
            else
            {
                if (list == null)
                {
                    cache.Entries.Add(hash, list = new List<SongEntry>());
                }

                list.Add(entry);
                ++_progress.Count;
            }
            return true;
        }

        protected override void AddUpdates(UpdateGroup group, Dictionary<string, List<YARGDTAReader>> nodes, bool removeEntries)
        {
            foreach (var node in nodes)
            {
                var update = new SongUpdate(group, node.Key, group.DTALastWrite, node.Value.ToArray());
                group.Updates.Add(node.Key, update);

                if (removeEntries)
                {
                    RemoveCONEntry(node.Key);
                }

                if (!updates.TryGetValue(node.Key, out var list))
                {
                    updates.Add(node.Key, list = new());
                }
                list.Add(update);
            }
            updateGroups.Add(group);
        }

        protected override void TraverseDirectory(FileCollector collector, IniGroup group, PlaylistTracker tracker)
        {
            foreach (var subDirectory in collector.subDirectories)
            {
                ScanDirectory(subDirectory, group, tracker);
            }

            foreach (var file in collector.subfiles)
            {
                ScanFile(file, group, ref tracker);
            }
        }

        protected override void SortEntries(InstrumentCategory[] instruments)
        {
            foreach (var node in cache.Entries)
            {
                foreach (var entry in node.Value)
                {
                    CategorySorter<string,     TitleConfig>.      Add(entry, cache.Titles);
                    CategorySorter<SortString, ArtistConfig>.     Add(entry, cache.Artists);
                    CategorySorter<SortString, AlbumConfig>.      Add(entry, cache.Albums);
                    CategorySorter<SortString, GenreConfig>.      Add(entry, cache.Genres);
                    CategorySorter<string,     YearConfig>.       Add(entry, cache.Years);
                    CategorySorter<SortString, CharterConfig>.    Add(entry, cache.Charters);
                    CategorySorter<SortString, PlaylistConfig>.   Add(entry, cache.Playlists);
                    CategorySorter<SortString, SourceConfig>.     Add(entry, cache.Sources);
                    CategorySorter<string,     ArtistAlbumConfig>.Add(entry, cache.ArtistAlbums);
                    CategorySorter<string,     SongLengthConfig>. Add(entry, cache.SongLengths);
                    CategorySorter<DateTime,   DateAddedConfig>.  Add(entry, cache.DatesAdded);

                    foreach (var instrument in instruments)
                        instrument.Add(entry);
                }
            }
        }

        protected override void Deserialize(FileStream stream)
        {
            CategoryCacheStrings strings = new(stream, false);
            RunEntryTasks(stream, strings, ReadIniGroup);
            RunCONTasks(stream, ReadUpdateDirectory);
            RunCONTasks(stream, ReadUpgradeDirectory);
            RunCONTasks(stream, ReadUpgradeCON);
            RunEntryTasks(stream, strings, ReadPackedCONGroup);
            RunEntryTasks(stream, strings, ReadUnpackedCONGroup);
        }

        protected override void Deserialize_Quick(FileStream stream)
        {
            CategoryCacheStrings strings = new(stream, false);
            RunEntryTasks(stream, strings, QuickReadIniGroup);

            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                stream.Position += length;
            }

            RunCONTasks(stream, QuickReadUpgradeDirectory);
            RunCONTasks(stream, QuickReadUpgradeCON);
            RunEntryTasks(stream, strings, QuickReadCONGroup);
            RunEntryTasks(stream, strings, QuickReadExtractedCONGroup);
        }

        protected override void AddUpgrade(string name, YARGDTAReader? reader, IRBProUpgrade upgrade)
        {
            upgrades[name] = new(reader, upgrade);
        }

        protected override void AddPackedCONGroup(PackedCONGroup group)
        {
            conGroups.Add(group);
        }

        protected override void AddUnpackedCONGroup(UnpackedCONGroup group)
        {
            extractedConGroups.Add(group);
        }

        protected override void AddUpgradeGroup(UpgradeGroup group)
        {
            upgradeGroups.Add(group);
        }

        protected override void RemoveCONEntry(string shortname)
        {
            foreach (var group in conGroups)
            {
                if (group.RemoveEntries(shortname))
                {
                    YargTrace.DebugInfo($"{group.Location} - {shortname} pending rescan");
                }
            }

            foreach (var group in extractedConGroups)
            {
                if (group.RemoveEntries(shortname))
                {
                    YargTrace.DebugInfo($"{group.Location} - {shortname} pending rescan");
                }
            }
        }

        protected override bool CanAddUpgrade(string shortname, DateTime lastUpdated)
        {
            return CanAddUpgrade(upgradeGroups, shortname, lastUpdated) ?? false;
        }

        protected override bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastUpdated)
        {
            var result = CanAddUpgrade(conGroups, shortname, lastUpdated);
            if (result != null)
            {
                return (bool) result;
            }
            return CanAddUpgrade(upgradeGroups, shortname, lastUpdated) ?? false;
        }

        protected override bool FindOrMarkDirectory(string directory)
        {
            lock (directory)
            {
                if (!preScannedDirectories.Add(directory))
                {
                    return false;
                }
                _progress.NumScannedDirectories++;
                return true;
            }
        }

        protected override bool FindOrMarkFile(string file)
        {
            lock (preScannedFiles)
            {
                return preScannedFiles.Add(file);
            }
        }

        protected override void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badSongs)
            {
                badSongs.Add(filePath, err);
                _progress.BadSongCount++;
            }
        }

        protected override void AddInvalidSong(string name)
        {
            lock (invalidSongsInCache)
            {
                invalidSongsInCache.Add(name);
            }
        }

        protected override PackedCONGroup? FindCONGroup(string filename)
        {
            return conGroups.Find(node => node.Location == filename);
        }

        private void ReadIniGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                return;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                ReadIniEntry(directory, group, entryReader, strings);
            }
        }

        private void ReadPackedCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader, out string filename);
            if (group != null)
            {
                ReadCONGroup(reader, (string name, int index, BinaryReader entryReader) => group.ReadEntry(name, index, upgrades, entryReader, strings));
            }
        }

        private void ReadUnpackedCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(reader, out string directory);
            if (group != null)
            {
                ReadCONGroup(reader, (string name, int index, BinaryReader entryReader) => group.ReadEntry(name, index, upgrades, entryReader, strings));
            }
        }

        private void QuickReadIniGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                QuickReadIniEntry(directory, entryReader, strings);
            }
        }

        private void QuickReadCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                AddEntry(PackedRBCONEntry.LoadFromCache_Quick(group.CONFile, name, upgrades, entryReader, strings));
            }
        }

        private void QuickReadExtractedCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            var dta = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "songs.dta"), reader);
            // Lack of null check of dta by design

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                AddEntry(UnpackedRBCONEntry.LoadFromCache_Quick(directory, dta, name, upgrades, entryReader, strings));
            }
        }

        private static void RunCONTasks(FileStream stream, Action<BinaryReader> func)
        {
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var reader = BinaryReaderExtensions.Load(stream, length);
                func(reader);
            }
        }

        private static void RunEntryTasks(FileStream stream, CategoryCacheStrings strings, Action<BinaryReader, CategoryCacheStrings> func)
        {
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var reader = BinaryReaderExtensions.Load(stream, length);
                func(reader, strings);
            }
        }
    }
}
