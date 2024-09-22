using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal sealed class SequentialCacheHandler : CacheHandler
    {
        internal SequentialCacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
            : base(baseDirectories, allowDuplicates, fullDirectoryPlaylists) { }

        protected override void FindNewEntries()
        {
            var tracker = new PlaylistTracker(fullDirectoryPlaylists, null);
            foreach (var group in iniGroups)
            {
                var dirInfo = new DirectoryInfo(group.Directory);
                ScanDirectory(dirInfo, group, tracker);
            }

            foreach (var group in conGroups)
            {
                if (group.LoadSongs(out var container))
                {
                    ScanCONGroup(group, ref container, ScanPackedCONNode);
                }
                group.DisposeStreamAndSongDTA();
            }

            foreach (var group in extractedConGroups)
            {
                if (group.LoadDTA(out var container))
                {
                    ScanCONGroup(group, ref container, ScanUnpackedCONNode);
                }
                group.Dispose();
            }

            foreach (var group in conGroups)
            {
                group.DisposeUpgradeDTA();
            }
        }

        protected override void TraverseDirectory(in FileCollection collection, IniGroup group, PlaylistTracker tracker)
        {
            foreach (var subDirectory in collection.SubDirectories)
            {
                ScanDirectory(subDirectory.Value, group, tracker);
            }

            foreach (var file in collection.Subfiles)
            {
                ScanFile(file.Value, group, in tracker);
            }
        }

        protected override void SortEntries()
        {
            foreach (var node in cache.Entries)
            {
                foreach (var entry in node.Value)
                {
                    CategorySorter<string, TitleConfig>.Add(entry, cache.Titles);
                    CategorySorter<SortString, ArtistConfig>.Add(entry, cache.Artists);
                    CategorySorter<SortString, AlbumConfig>.Add(entry, cache.Albums);
                    CategorySorter<SortString, GenreConfig>.Add(entry, cache.Genres);
                    CategorySorter<string, YearConfig>.Add(entry, cache.Years);
                    CategorySorter<SortString, CharterConfig>.Add(entry, cache.Charters);
                    CategorySorter<SortString, PlaylistConfig>.Add(entry, cache.Playlists);
                    CategorySorter<SortString, SourceConfig>.Add(entry, cache.Sources);
                    CategorySorter<SortString, ArtistAlbumConfig>.Add(entry, cache.ArtistAlbums);
                    CategorySorter<string, SongLengthConfig>.Add(entry, cache.SongLengths);
                    CategorySorter<DateTime, DateAddedConfig>.Add(entry, cache.DatesAdded);
                    InstrumentSorter.Add(entry, cache.Instruments);
                }
            }
        }

        protected override void Deserialize(UnmanagedMemoryStream stream)
        {
            CategoryCacheStrings strings = new(stream, false);
            RunEntryTasks(stream, strings, ReadIniGroup);
            RunCONTasks(stream, ReadUpdateDirectory);
            RunCONTasks(stream, ReadUpgradeDirectory);
            RunCONTasks(stream, ReadUpgradeCON);
            RunEntryTasks(stream, strings, ReadPackedCONGroup);
            RunEntryTasks(stream, strings, ReadUnpackedCONGroup);
        }

        protected override void Deserialize_Quick(UnmanagedMemoryStream stream)
        {
            CategoryCacheStrings strings = new(stream, false);
            RunEntryTasks(stream, strings, QuickReadIniGroup);

            int skipLength = stream.Read<int>(Endianness.Little);
            stream.Position += skipLength;

            RunCONTasks(stream, QuickReadUpgradeDirectory);
            RunCONTasks(stream, QuickReadUpgradeCON);
            RunEntryTasks(stream, strings, QuickReadCONGroup);
            RunEntryTasks(stream, strings, QuickReadExtractedCONGroup);
        }

        protected override void AddUpdate(string name, DateTime dtaLastWrite, SongUpdate update)
        {
            if (!updates.TryGetValue(name, out var list))
            {
                updates.Add(name, list = new());
            }
            list.Add(dtaLastWrite, update);
        }

        protected override void AddUpgrade(string name, in YARGTextContainer<byte> container, RBProUpgrade upgrade)
        {
            upgrades[name] = new(container, upgrade);
        }

        protected override void AddPackedCONGroup(PackedCONGroup group)
        {
            conGroups.Add(group);
        }

        protected override void AddUnpackedCONGroup(UnpackedCONGroup group)
        {
            extractedConGroups.Add(group);
        }

        protected override void AddUpdateGroup(UpdateGroup group)
        {
            updateGroups.Add(group);
        }

        protected override void AddUpgradeGroup(UpgradeGroup group)
        {
            upgradeGroups.Add(group);
        }

        protected override void AddCollectionToCache(in FileCollection collection)
        {
            collectionCache.Add(collection.Directory.FullName, collection);
        }

        protected override void RemoveCONEntry(string shortname)
        {
            foreach (var group in conGroups)
            {
                if (group.RemoveEntries(shortname))
                {
                    YargLogger.LogFormatTrace("{0} - {1} pending rescan", group.Location, item2: shortname);
                }
            }

            foreach (var group in extractedConGroups)
            {
                if (group.RemoveEntries(shortname))
                {
                    YargLogger.LogFormatTrace("{0} - {1} pending rescan", group.Location, item2: shortname);
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

        protected override Dictionary<string, Dictionary<string, FileInfo>> MapUpdateFiles(in FileCollection collection)
        {
            Dictionary<string, Dictionary<string, FileInfo>> mapping = new();
            foreach (var dir in collection.SubDirectories)
            {
                var infos = new Dictionary<string, FileInfo>();
                foreach (var file in dir.Value.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    infos[file.Name] = file;
                }
                mapping[dir.Key] = infos;
            }
            return mapping;
        }

        protected override PackedCONGroup? FindCONGroup(string filename)
        {
            return conGroups.Find(node => node.Location == filename);
        }

        protected override void CleanupDuplicates()
        {
            foreach (var entry in duplicatesToRemove)
            {
                if (TryRemove<IniGroup, IniSubEntry>(iniGroups, entry))
                {
                    continue;
                }

                if (TryRemove<PackedCONGroup, RBCONEntry>(conGroups, entry))
                {
                    continue;
                }

                TryRemove<UnpackedCONGroup, RBCONEntry>(extractedConGroups, entry);
            }
        }

        private void ScanCONGroup<TGroup>(TGroup group, ref YARGTextContainer<byte> container, Action<TGroup, string, int, YARGTextContainer<byte>> func)
            where TGroup : CONGroup
        {
            try
            {
                Dictionary<string, int> indices = new();
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    if (indices.TryGetValue(name, out int index))
                    {
                        ++index;
                    }
                    indices[name] = index;

                    func(group, name, index, container);
                    YARGDTAReader.EndNode(ref container);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning CON group {group.Location}!");
            }
        }

        private void ReadIniGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            string directory = stream.ReadString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                return;
            }

            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var slice = stream.Slice(length);
                ReadIniEntry(group, directory, slice, strings);
            }
        }

        private void ReadPackedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(stream);
            if (group != null)
            {
                ReadCONGroup(stream, (string name, int index, UnmanagedMemoryStream slice) => group.ReadEntry(name, index, upgrades, slice, strings));
            }
        }

        private void ReadUnpackedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(stream);
            if (group != null)
            {
                ReadCONGroup(stream, (string name, int index, UnmanagedMemoryStream slice) => group.ReadEntry(name, index, upgrades, slice, strings));
            }
        }

        private void QuickReadIniGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            string directory = stream.ReadString();
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var slice = stream.Slice(length);
                QuickReadIniEntry(directory, slice, strings);
            }
        }

        private void QuickReadCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(stream);
            if (group == null)
                return;

            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                string name = stream.ReadString();
                // index
                stream.Position += 4;

                int length = stream.Read<int>(Endianness.Little);
                var slice = stream.Slice(length);
                AddEntry(PackedRBCONEntry.LoadFromCache_Quick(in group.ConFile, name, upgrades, slice, strings));
            }
        }

        private void QuickReadExtractedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            string directory = stream.ReadString();
            var dta = new AbridgedFileInfo(Path.Combine(directory, "songs.dta"), stream);

            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                string name = stream.ReadString();
                // index
                stream.Position += 4;

                int length = stream.Read<int>(Endianness.Little);
                var slice = stream.Slice(length);
                AddEntry(UnpackedRBCONEntry.LoadFromCache_Quick(directory, dta, name, upgrades, slice, strings));
            }
        }

        private static void RunCONTasks(UnmanagedMemoryStream stream, Action<UnmanagedMemoryStream> func)
        {
            int sectionLength = stream.Read<int>(Endianness.Little);
            var sectionSlice = stream.Slice(sectionLength);

            int count = sectionSlice.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int groupLength = sectionSlice.Read<int>(Endianness.Little);
                var groupSlice = sectionSlice.Slice(groupLength);
                func(groupSlice);
            }
        }

        private static void RunEntryTasks(UnmanagedMemoryStream stream, CategoryCacheStrings strings, Action<UnmanagedMemoryStream, CategoryCacheStrings> func)
        {
            int sectionLength = stream.Read<int>(Endianness.Little);
            var sectionSlice = stream.Slice(sectionLength);

            int count = sectionSlice.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int groupLength = sectionSlice.Read<int>(Endianness.Little);
                var groupSlice = sectionSlice.Slice(groupLength);
                func(groupSlice, strings);
            }
        }
    }
}
