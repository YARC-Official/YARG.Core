using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private readonly HashSet<string> invalidSongsInCache = new();

        private static void RunCONTasks(FileStream stream, Action<YARGBinaryReader> func)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                func(reader);
            }
        }

        private static void RunEntryTasks(FileStream stream, CategoryCacheStrings strings, Action<YARGBinaryReader, CategoryCacheStrings> func)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                func(reader, strings);
            }
        }

        private static void AddParallelCONTasks(FileStream stream, ref List<Task> conTasks, Action<YARGBinaryReader> func, ParallelExceptionTracker tracker)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                conTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        func(reader);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private static void AddParallelEntryTasks(FileStream stream, ref List<Task> entryTasks, CategoryCacheStrings strings, Action<YARGBinaryReader, List<Task>, CategoryCacheStrings, ParallelExceptionTracker> func, ParallelExceptionTracker tracker)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                entryTasks.Add(Task.Run(() => {
                    List<Task> tasks = new();
                    try
                    {
                        func(reader, tasks, strings, tracker);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                    Task.WaitAll(tasks.ToArray());
                }));
            }
        }

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        /// 
        /// </summary>
        private const int MIN_CACHEFILESIZE = 92;

        private FileStream? CheckCacheFile()
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargTrace.DebugInfo($"Cache invalid or not found");
                return null;
            }

            FileStream fs = new(cacheLocation, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CACHE_VERSION)
            {
                YargTrace.DebugInfo($"Cache outdated");
                fs.Dispose();
                return null;
            }
            return fs;
        }

        private void Deserialize()
        {
            Progress = ScanProgress.LoadingCache;
            using var stream = CheckCacheFile();
            if (stream == null)
                return;

            YargTrace.DebugInfo("Full Read start");
            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                List<Task> entryTasks = new();
                List<Task> conTasks = new();
                ParallelExceptionTracker tracker = new();

                try
                {
                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadIniGroup_Parallel, tracker);
                    AddParallelCONTasks(stream, ref conTasks, ReadUpdateDirectory, tracker);
                    AddParallelCONTasks(stream, ref conTasks, ReadUpgradeDirectory, tracker);
                    AddParallelCONTasks(stream, ref conTasks, ReadUpgradeCON, tracker);
                    Task.WaitAll(conTasks.ToArray());

                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadCONGroup_Parallel, tracker);
                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadExtractedCONGroup_Parallel, tracker);
                    Task.WaitAll(entryTasks.ToArray());
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                    // Must ensure task completion
                    Task.WaitAll(conTasks.ToArray());
                    Task.WaitAll(entryTasks.ToArray());
                }

                if (tracker.IsSet())
                    throw tracker.Exception!;
            }
            else
            {
                RunEntryTasks(stream, strings, ReadIniGroup);
                RunCONTasks(stream, ReadUpdateDirectory);
                RunCONTasks(stream, ReadUpgradeDirectory);
                RunCONTasks(stream, ReadUpgradeCON);
                RunEntryTasks(stream, strings, ReadCONGroup);
                RunEntryTasks(stream, strings, ReadExtractedCONGroup);
            }
            YargTrace.DebugInfo($"Ini Entries read: {_count}");
        }

        private bool Deserialize_Quick()
        {
            Progress = ScanProgress.LoadingCache;
            using var stream = CheckCacheFile();
            if (stream == null)
                return false;

            YargTrace.DebugInfo("Quick Read start");
            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                List<Task> entryTasks = new();
                List<Task> conTasks = new();
                ParallelExceptionTracker tracker = new();

                try
                {
                    AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadIniGroup_Parallel, tracker);

                    int count = stream.ReadInt32LE();
                    for (int i = 0; i < count; ++i)
                    {
                        int length = stream.ReadInt32LE();
                        stream.Position += length;
                    }

                    AddParallelCONTasks(stream, ref conTasks, QuickReadUpgradeDirectory, tracker);
                    AddParallelCONTasks(stream, ref conTasks, QuickReadUpgradeCON, tracker);
                    Task.WaitAll(conTasks.ToArray());

                    AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadCONGroup_Parallel, tracker);
                    AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadExtractedCONGroup_Parallel, tracker);
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                    Task.WaitAll(conTasks.ToArray());
                }
                Task.WaitAll(entryTasks.ToArray());
            }
            else
            {
                RunEntryTasks(stream, strings, QuickReadIniGroup);
                YargTrace.DebugInfo($"Ini Entries quick read: {_count}");

                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    stream.Position += length;
                }

                RunCONTasks(stream, QuickReadUpgradeDirectory);
                RunCONTasks(stream, QuickReadUpgradeCON);
                RunEntryTasks(stream, strings, QuickReadCONGroup);
                RunEntryTasks(stream, strings, QuickReadExtractedCONGroup);
            }
            YargTrace.DebugInfo($"Total Entries: {_count}");
            return true;
        }

        private void Serialize()
        {
            Progress = ScanProgress.WritingCache;
            using var writer = new BinaryWriter(new FileStream(cacheLocation, FileMode.Create, FileAccess.Write));
            Dictionary<SongMetadata, CategoryCacheWriteNode> nodes = new();

            writer.Write(CACHE_VERSION);

            cache.titles.WriteToCache(writer, ref nodes);
            cache.artists.WriteToCache(writer, ref nodes);
            cache.albums.WriteToCache(writer, ref nodes);
            cache.genres.WriteToCache(writer, ref nodes);
            cache.years.WriteToCache(writer, ref nodes);
            cache.charters.WriteToCache(writer, ref nodes);
            cache.playlists.WriteToCache(writer, ref nodes);
            cache.sources.WriteToCache(writer, ref nodes);

            List<KeyValuePair<string, PackedCONGroup>> upgradeCons = new();
            List<KeyValuePair<string, PackedCONGroup>> entryCons = new();
            foreach (var group in conGroups.Values)
            {
                if (group.Value.Upgrades.Count > 0)
                    upgradeCons.Add(group);

                if (group.Value.EntryCount > 0)
                    entryCons.Add(group);
            }

            ICacheGroup.SerializeGroups(iniGroups, writer, nodes);
            IModificationGroup.SerializeGroups(updateGroups.Values, writer);
            IModificationGroup.SerializeGroups(upgradeGroups.Values, writer);
            IModificationGroup.SerializeGroups(upgradeCons, writer);
            ICacheGroup.SerializeGroups(entryCons, writer, nodes);
            ICacheGroup.SerializeGroups(extractedConGroups.Values, writer, nodes);
        }

        private void ReadIniEntry(string baseDirectory, IniGroup group, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = SongMetadata.IniFromCache(baseDirectory, reader, strings);
            if (entry == null)
            {
                YargTrace.DebugInfo($"Ini entry invalid {baseDirectory}");
                return;
            }

            MarkDirectory(entry.Directory);
            AddEntry(entry);
            group.AddEntry(entry);
        }

        private void ReadUpdateDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    YargTrace.DebugInfo($"Update Directory added {directory}");
                    MarkDirectory(directory);
                    CreateUpdateGroup(directory, dta);

                    if (dta.LastWriteTime == dtaLastWrite)
                        return;
                }
            }

            for (int i = 0; i < count; i++)
                AddInvalidSong(reader.ReadLEBString());
        }

        private void ReadUpgradeDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    YargTrace.DebugInfo($"Upgrade Directory added {directory}");
                    MarkDirectory(directory);
                    var group = CreateUpgradeGroup(directory, dta);

                    if (group != null && dta.LastWriteTime == dtaLastWrite)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadLEBString();
                            var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.upgrades.TryGetValue(name, out var upgrade) || upgrade!.LastWrite != lastWrite)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadLEBString());
                reader.Position += SongMetadata.SIZEOF_DATETIME;
            }
        }

        private void ReadUpgradeCON(YARGBinaryReader cacheReader)
        {
            string filename = cacheReader.ReadLEBString();
            var conLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            var dtaLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int count = cacheReader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(filename) != null && CreateCONGroup(filename, out var group))
            {
                YargTrace.DebugInfo($"CON added in upgrade loop {filename}");
                conGroups.Add(filename, group!);

                var reader = group!.LoadUpgrades();
                if (reader != null)
                {
                    AddCONUpgrades(group, reader);

                    if (group.UpgradeDTALastWrite == dtaLastWrite)
                    {
                        if (group.LastWrite != conLastWrite)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                string name = cacheReader.ReadLEBString();
                                var lastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
                                if (group.Upgrades[name].LastWrite != lastWrite)
                                    AddInvalidSong(name);
                            }
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadLEBString());
                cacheReader.Position += SongMetadata.SIZEOF_DATETIME;
            }
        }

        private PackedCONGroup? ReadCONGroupHeader(YARGBinaryReader reader, out string filename)
        {
            filename = reader.ReadLEBString();
            // Functions as a "check base directory" call
            if (GetBaseIniGroup(filename) == null)
            {
                YargTrace.DebugInfo($"CON outside base directories : {filename}");
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                FileInfo info = new(filename);
                if (!info.Exists)
                {
                    YargTrace.DebugInfo($"CON no longer found: {filename}");
                    return null;
                }

                MarkFile(filename);

                var files = CONFileHandler.TryParseListings(info.FullName);
                if (files == null)
                {
                    YargTrace.DebugInfo($"CON could not be loaded: {filename}");
                    return null;
                }

                group = new(files, info.LastWriteTime);
                YargTrace.DebugInfo($"CON added in main loop {filename}");
                conGroups.Add(filename, group);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
            {
                YargTrace.DebugInfo($"CON songs.dta was missing or updated");
                return null;
            }
            return group;
        }

        private UnpackedCONGroup? ReadExtractedCONGroupHeader(YARGBinaryReader reader, out string directory)
        {
            directory = reader.ReadLEBString();
            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) == null)
            {
                YargTrace.DebugInfo($"EXCON outside base directories : {directory}");
                return null;
            }

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
            {
                YargTrace.DebugInfo($"EXCON dta missing");
                return null;
            }

            UnpackedCONGroup group = new(dtaInfo);
            YargTrace.DebugInfo($"EXCON added in main loop {directory}");
            MarkDirectory(directory);
            extractedConGroups.Add(directory, group!);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                YargTrace.DebugInfo($"EXCON dta updated, needs rescan");
                return null;
            }
            return group;
        }

        private void QuickReadIniEntry(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = SongMetadata.IniFromCache_Quick(baseDirectory, reader, strings);
            if (entry != null)
                AddEntry(entry);
            else
                YargTrace.DebugInfo($"Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
        }

        private void QuickReadUpgradeDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            UpgradeGroup group = new(dtaLastWrite);
            upgradeGroups.Add(directory, group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadLEBString();
                var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                IRBProUpgrade upgrade = new UnpackedRBProUpgrade(filename, lastWrite);
                group.upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        private void QuickReadUpgradeCON(YARGBinaryReader reader)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 2 * SongMetadata.SIZEOF_DATETIME;
            int count = reader.ReadInt32();

            if (CreateCONGroup(filename, out var group))
            {
                conGroups.Add(filename, group!);

                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    var listing = CONFileHandler.TryGetListing(group!.Files, $"songs_upgrades/{name}_plus.mid");

                    IRBProUpgrade upgrade = new PackedRBProUpgrade(listing, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());

                    IRBProUpgrade upgrade = new PackedRBProUpgrade(null, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
        }

        private PackedCONGroup? QuickReadCONGroupHeader(YARGBinaryReader reader)
        {
            string filename = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                if (!CreateCONGroup(filename, out group))
                {
                    YargTrace.DebugInfo($"CON was not found: {filename}");
                    return null;
                }
                conGroups.Add(filename, group!);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
            {
                YargTrace.DebugInfo($"Con songs.dta missing or needs rescan: {filename}");
                return null;
            }
            return group;
        }

        private AbridgedFileInfo? QuickReadExtractedCONGroupHeader(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists || dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                YargTrace.DebugInfo($"EXCON dta missing");
                return null;
            }
            return new(dtaInfo);
        }

        private bool CreateCONGroup(string filename, out PackedCONGroup? group)
        {
            group = null;

            FileInfo info = new(filename);
            if (!info.Exists)
                return false;

            MarkFile(filename);

            var files = CONFileHandler.TryParseListings(filename);
            if (files == null)
                return false;

            group = new(files, info.LastWriteTime);
            return true;
        }

        private bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conGroups.Lock) return conGroups.Values.TryGetValue(filename, out group);
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private void MarkFile(string file)
        {
            YargTrace.DebugInfo($"Marked CON {file}");
            lock (fileLock) preScannedFiles.Add(file);
        }

        private void AddInvalidSong(string name)
        {
            YargTrace.DebugInfo($"Invalidated {name}");
            lock (invalidLock) invalidSongsInCache.Add(name);
        }
    }
}
