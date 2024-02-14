using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        public const int SIZEOF_DATETIME = 8;
        private readonly HashSet<string> invalidSongsInCache = new();

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

        private static void AddParallelCONTasks(FileStream stream, ref List<Task> conTasks, Action<BinaryReader> func, ParallelExceptionTracker tracker)
        {
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var reader = BinaryReaderExtensions.Load(stream, length);
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

        private static void AddParallelEntryTasks(FileStream stream, ref List<Task> entryTasks, CategoryCacheStrings strings, Action<BinaryReader, List<Task>, CategoryCacheStrings, ParallelExceptionTracker> func, ParallelExceptionTracker tracker)
        {
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = stream.Read<int>(Endianness.Little);
                var reader = BinaryReaderExtensions.Load(stream, length);
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
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        /// 
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        private FileStream? CheckCacheFile(string cacheLocation)
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargTrace.DebugInfo($"Cache invalid or not found");
                return null;
            }

            var fs = new FileStream(cacheLocation, FileMode.Open, FileAccess.Read);
            using var counter = DisposableCounter.Wrap(fs);
            if (fs.Read<int>(Endianness.Little) != CACHE_VERSION)
            {
                YargTrace.DebugInfo($"Cache outdated");
                return null;
            }

            if (fs.ReadBoolean() != fullDirectoryPlaylists)
            {
                YargTrace.DebugInfo($"FullDirectoryFlag flipped");
                return null;
            }

            return counter.Release();
        }

        private void Deserialize(string cacheLocation, bool multithreading)
        {
            using var stream = CheckCacheFile(cacheLocation);
            if (stream == null)
                return;

            YargTrace.DebugInfo("Full Read start");
            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                var tracker = new ParallelExceptionTracker();
                var entryTasks = new List<Task>();
                var conTasks = new List<Task>();

                try
                {
                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadIniGroup_Parallel, tracker);
                    AddParallelCONTasks(stream, ref conTasks, (BinaryReader reader) => ReadUpdateDirectory(reader, CreateUpdateGroup_Parallel), tracker);
                    AddParallelCONTasks(stream, ref conTasks, ReadUpgradeDirectory, tracker);
                    AddParallelCONTasks(stream, ref conTasks, ReadUpgradeCON, tracker);
                    Task.WaitAll(conTasks.ToArray());

                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadCONGroup_Parallel, tracker);
                    AddParallelEntryTasks(stream, ref entryTasks, strings, ReadExtractedCONGroup_Parallel, tracker);
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                    Task.WaitAll(conTasks.ToArray());
                }
                Task.WaitAll(entryTasks.ToArray());

                if (tracker.IsSet())
                    throw tracker;
            }
            else
            {
                RunEntryTasks(stream, strings, ReadIniGroup);
                RunCONTasks(stream, (BinaryReader reader) => ReadUpdateDirectory(reader, CreateUpdateGroup));
                RunCONTasks(stream, ReadUpgradeDirectory);
                RunCONTasks(stream, ReadUpgradeCON);
                RunEntryTasks(stream, strings, ReadCONGroup);
                RunEntryTasks(stream, strings, ReadExtractedCONGroup);
            }
            YargTrace.DebugInfo($"Ini Entries read: {_progress.Count}");
        }

        private bool Deserialize_Quick(string cacheLocation, bool multithreading)
        {
            using var stream = CheckCacheFile(cacheLocation);
            if (stream == null)
                return false;

            YargTrace.DebugInfo("Quick Read start");
            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                var tracker = new ParallelExceptionTracker();
                var entryTasks = new List<Task>();
                var conTasks = new List<Task>();

                try
                {
                    AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadIniGroup_Parallel, tracker);

                    int count = stream.Read<int>(Endianness.Little);
                    for (int i = 0; i < count; ++i)
                    {
                        int length = stream.Read<int>(Endianness.Little);
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

                if (tracker.IsSet())
                    throw tracker;
            }
            else
            {
                RunEntryTasks(stream, strings, QuickReadIniGroup);
                YargTrace.DebugInfo($"Ini Entries quick read: {_progress.Count}");

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
            YargTrace.DebugInfo($"Total Entries: {_progress.Count}");
            return true;
        }

        private void Serialize(string cacheLocation)
        {
            _progress.Stage = ScanStage.WritingCache;
            using var writer = new BinaryWriter(new FileStream(cacheLocation, FileMode.Create, FileAccess.Write));
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            writer.Write(CACHE_VERSION);
            writer.Write(fullDirectoryPlaylists);

            CategoryWriter.WriteToCache(writer, cache.Titles   , SongAttribute.Name,     ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Artists  , SongAttribute.Artist,   ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Albums   , SongAttribute.Album,    ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Genres   , SongAttribute.Genre,    ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Years    , SongAttribute.Year,     ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Charters , SongAttribute.Charter,  ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Playlists, SongAttribute.Playlist, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Sources  , SongAttribute.Source,   ref nodes);

            List<PackedCONGroup> upgradeCons = new();
            List<PackedCONGroup> entryCons = new();
            foreach (var group in conGroups.Values)
            {
                if (group.Upgrades.Count > 0)
                    upgradeCons.Add(group);

                if (group.Count > 0)
                    entryCons.Add(group);
            }

            ICacheGroup<IniSubEntry>.SerializeGroups(iniGroups, writer, nodes);
            IModificationGroup.SerializeGroups(updateGroups.Values, writer);
            IModificationGroup.SerializeGroups(upgradeGroups.Values, writer);
            IModificationGroup.SerializeGroups(upgradeCons, writer);
            ICacheGroup<RBCONEntry>.SerializeGroups(entryCons, writer, nodes);
            ICacheGroup<RBCONEntry>.SerializeGroups(extractedConGroups.Values, writer, nodes);
        }

        private void ReadIniEntry(string baseDirectory, IniGroup group, BinaryReader reader, CategoryCacheStrings strings)
        {
            bool isSngEntry = reader.ReadBoolean();
            var entry = isSngEntry ?
                SngEntry.TryLoadFromCache(baseDirectory, reader, strings) :
                UnpackedIniEntry.TryLoadFromCache(baseDirectory, reader, strings);

            if (entry == null)
            {
                YargTrace.DebugInfo($"Ini entry invalid {baseDirectory}");
                return;
            }

            string root = entry.Directory;
            if (!isSngEntry)
            {
                if (Directory.EnumerateDirectories(root).Any())
                {
                    AddToBadSongs(root, ScanResult.LooseChart_Warning);
                }
                MarkDirectory(root);
            }
            else
            {
                MarkFile(root);
            }

            AddEntry(entry);
            group.AddEntry(entry);
        }

        private void ReadUpdateDirectory(BinaryReader reader, Func<DirectoryInfo, AbridgedFileInfo, bool, UpdateGroup?> updateFunc)
        {
            string directory = reader.ReadString();
            var dtaLastWritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                var dtaInfo = new FileInfo(Path.Combine(directory, "songs_updates.dta"));
                if (dtaInfo.Exists)
                {
                    MarkDirectory(directory);

                    var abridged = new AbridgedFileInfo(dtaInfo, false);
                    var dirInfo = new DirectoryInfo(directory);
                    var group = updateFunc(dirInfo, abridged, false);
                    if (group != null && abridged.LastUpdatedTime == dtaLastWritten)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            if (group.Updates.TryGetValue(name, out var update))
                            {
                                if (!update.Validate(reader))
                                {
                                    AddInvalidSong(name);
                                }
                            }
                            else
                            {
                                AddInvalidSong(name);
                                SongUpdate.SkipRead(reader);
                            }
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                SongUpdate.SkipRead(reader);
            }
        }

        private void ReadUpgradeDirectory(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var dtaLastWrritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                var dtaInfo = new FileInfo(Path.Combine(directory, "upgrades.dta"));
                if (dtaInfo.Exists)
                {
                    MarkDirectory(directory);

                    var abridged = new AbridgedFileInfo(dtaInfo, false);
                    var group = CreateUpgradeGroup(directory, abridged, false);
                    if (group != null && abridged.LastUpdatedTime == dtaLastWrritten)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            var lastUpdated = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.Upgrades.TryGetValue(name, out var upgrade) || upgrade!.LastUpdatedTime != lastUpdated)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                reader.Move(SIZEOF_DATETIME);
            }
        }

        private void ReadUpgradeCON(BinaryReader reader)
        {
            string filename = reader.ReadString();
            var conLastUpdated = DateTime.FromBinary(reader.ReadInt64());
            var dtaLastWritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup != null)
            {
                // Make playlist as the group is only made once
                string playlist = ConstructPlaylist(filename, baseGroup.Directory);
                var group = CreateCONGroup(filename, playlist);
                if (group == null)
                {
                    goto Invalidate;
                }

                conGroups.Add(group);

                if (TryParseUpgrades(filename, group) && group.UpgradeDTALastWrite == dtaLastWritten)
                {
                    if (group.CONLastUpdated != conLastUpdated)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (group.Upgrades[name].LastUpdatedTime != lastWrite)
                            {
                                AddInvalidSong(name);
                            }
                        }
                    }
                    return;
                }
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                reader.Move(SIZEOF_DATETIME);
            }
        }

        private string ConstructPlaylist(string filename, string baseDirectory)
        {
            string directory = Path.GetDirectoryName(filename);
            if (directory.Length == baseDirectory.Length)
            {
                return "Unknown Playlist";
            }

            if (!fullDirectoryPlaylists)
            {
                return Path.GetFileName(directory);
            }
            return directory[(baseDirectory.Length + 1)..];
        }

        private PackedCONGroup? ReadCONGroupHeader(BinaryReader reader, out string filename)
        {
            filename = reader.ReadString();
            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            var group = FindCONGroup(filename);
            if (group == null)
            {
                var info = new FileInfo(filename);
                if (!info.Exists)
                {
                    return null;
                }

                MarkFile(filename);

                var abridged = new AbridgedFileInfo(info);
                var file = CONFile.TryLoadFile(abridged);
                if (file == null)
                {
                    return null;
                }

                string playlist = ConstructPlaylist(filename, baseGroup.Directory);
                group = new PackedCONGroup(file, abridged, playlist);
                conGroups.Add(group);
            }

            if (!group.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
            {
                return null;
            }
            return group;
        }

        private UnpackedCONGroup? ReadExtractedCONGroupHeader(BinaryReader reader, out string directory)
        {
            directory = reader.ReadString();
            var baseGroup = GetBaseIniGroup(directory);
            if (baseGroup == null)
            {
                return null;
            }

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
            {
                return null;
            }

            MarkDirectory(directory);

            string playlist = ConstructPlaylist(directory, baseGroup.Directory);
            var group = new UnpackedCONGroup(directory, dtaInfo, playlist);
            extractedConGroups.Add(group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                return null;
            }
            return group;
        }

        private void QuickReadIniEntry(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = reader.ReadBoolean() ?
                SngEntry.LoadFromCache_Quick(baseDirectory, reader, strings) :
                UnpackedIniEntry.IniFromCache_Quick(baseDirectory, reader, strings);

            if (entry != null)
            {
                AddEntry(entry);
            }
            else
            {
                YargTrace.LogError($"Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
            }
        }

        private void QuickReadUpgradeDirectory(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var dtaLastUpdated = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            var group = new UpgradeGroup(directory, dtaLastUpdated);
            upgradeGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                var info = new AbridgedFileInfo(filename, reader);
                IRBProUpgrade upgrade = new UnpackedRBProUpgrade(info);
                group.Upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        private void QuickReadUpgradeCON(BinaryReader reader)
        {
            string filename = reader.ReadString();
            reader.Move(2 * SIZEOF_DATETIME);
            int count = reader.ReadInt32();

            var group = CreateCONGroup(filename, string.Empty);
            if (group != null)
            {
                conGroups.Add(group);
            }

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                var listing = group?.CONFile.TryGetListing($"songs_upgrades/{name}_plus.mid");

                IRBProUpgrade upgrade = new PackedRBProUpgrade(listing, lastWrite);
                AddUpgrade(name, null, upgrade);
            }
        }

        private PackedCONGroup? QuickReadCONGroupHeader(BinaryReader reader)
        {
            string filename = reader.ReadString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());

            var group = FindCONGroup(filename);
            if (group == null)
            {
                group = CreateCONGroup(filename, string.Empty);
                if (group == null)
                {
                    return null;
                }

                conGroups.Add(group);
            }

            if (!group.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
            {
                return null;
            }
            return group;
        }

        private PackedCONGroup? CreateCONGroup(string filename, string defaultPlaylist)
        {
            var info = new FileInfo(filename);
            if (!info.Exists)
                return null;

            MarkFile(filename);

            var abridged = new AbridgedFileInfo(info);
            var conFile = CONFile.TryLoadFile(abridged);
            if (conFile == null)
                return null;

            return new PackedCONGroup(conFile, abridged, defaultPlaylist);
        }

        private PackedCONGroup? FindCONGroup(string filename)
        {
            lock (conGroups.Lock)
            {
                var index = conGroups.Values.FindIndex(node => node.Location == filename);
                if (index == -1)
                {
                    return null;
                }
                return conGroups.Values[index];
            }
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock)
            {
                preScannedDirectories.Add(directory);
                _progress.NumScannedDirectories++;
            }
        }

        private void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
        }

        private void AddInvalidSong(string name)
        {
            lock (invalidLock) invalidSongsInCache.Add(name);
        }
    }
}
