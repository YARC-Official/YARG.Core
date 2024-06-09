using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    internal sealed class ParallelCacheHandler : CacheHandler
    {
        private sealed class ParallelExceptionTracker : Exception
        {
            private readonly object _lock = new object();
            private Exception? _exception = null;

            public bool IsSet()
            {
                lock (_lock)
                    return _exception != null;
            }

            /// <summary>
            /// Once set, the exception can not be swapped.
            /// </summary>
            public void Set(Exception exception)
            {
                lock (_lock)
                    _exception ??= exception;
            }

            public Exception? Exception => _exception;

            public override IDictionary? Data => _exception?.Data;

            public override string Message => _exception?.Message ?? string.Empty;

            public override string StackTrace => _exception?.StackTrace ?? string.Empty;

            public override string ToString()
            {
                return _exception?.ToString() ?? string.Empty;
            }

            public override Exception? GetBaseException()
            {
                return _exception?.GetBaseException();
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                _exception?.GetObjectData(info, context);
            }
        }

        public ParallelCacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
            : base(baseDirectories, allowDuplicates, fullDirectoryPlaylists) { }

        protected override void FindNewEntries()
        {
            var tracker = new PlaylistTracker(fullDirectoryPlaylists);
            Parallel.ForEach(iniGroups, group =>
            {
                var dirInfo = new DirectoryInfo(group.Directory);
                ScanDirectory(dirInfo, group, tracker);
            });

            // Orders the updates from oldest to newest to apply more recent information last
            Parallel.ForEach(updates, node => node.Value.Sort());

            var conTasks = new Task[conGroups.Count + extractedConGroups.Count];
            int con = 0;
            foreach (var group in conGroups)
            {
                conTasks[con++] = Task.Run(() =>
                {
                    var reader = group.LoadSongs();
                    if (reader != null)
                    {
                        ScanCONGroup(group, reader, ScanPackedCONNode);
                    }
                    group.Stream?.Dispose();
                }
                );
            }

            foreach (var group in extractedConGroups)
            {
                conTasks[con++] = Task.Run(() =>
                {
                    var reader = group.LoadDTA();
                    if (reader != null)
                    {
                        ScanCONGroup(group, reader, ScanUnpackedCONNode);
                    }
                }
                );
            }

            Task.WaitAll(conTasks);
        }

        protected override void TraverseDirectory(FileCollector collector, IniGroup group, PlaylistTracker tracker)
        {
            var tasks = new Task[collector.subDirectories.Count + collector.subfiles.Count];
            int index = 0;
            foreach (var subDirectory in collector.subDirectories)
            {
                tasks[index++] = Task.Run(() => ScanDirectory(subDirectory, group, tracker));
            }

            foreach (var file in collector.subfiles)
            {
                tasks[index++] = Task.Run(() => ScanFile(file, group, ref tracker));
            }
            Task.WaitAll(tasks);
            foreach (var task in tasks) task.Dispose();
        }

        protected override bool AddEntry(SongEntry entry)
        {
            lock (cache.Entries)
            {
                return base.AddEntry(entry);
            }
        }

        protected override void AddUpdates(UpdateGroup group, Dictionary<string, List<YARGDTAReader>> nodes, bool removeEntries)
        {
            Parallel.ForEach(nodes, node =>
            {
                var update = new SongUpdate(group, node.Key, group.DTALastWrite, node.Value.ToArray());
                lock (group.Updates)
                {
                    group.Updates.Add(node.Key, update);
                }

                if (removeEntries)
                {
                    RemoveCONEntry(node.Key);
                }

                lock (updates)
                {
                    if (!updates.TryGetValue(node.Key, out var list))
                    {
                        updates.Add(node.Key, list = new());
                    }
                    list.Add(update);
                }
            });

            lock (updateGroups)
            {
                updateGroups.Add(group);
            }
        }

        private void ScanCONGroup<TGroup>(TGroup group, YARGDTAReader reader, Action<TGroup, string, int, YARGDTAReader> func)
            where TGroup : CONGroup
        {
            try
            {
                List<Task> tasks = new();
                TraverseCONGroup(reader, (string name, int index) =>
                {
                    var node = reader.Clone();
                    tasks.Add(Task.Run(() => func(group, name, index, node)));
                });
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning CON group {group.Location}!");
            }
        }

        protected override void SortEntries()
        {
            Parallel.ForEach(cache.Entries, node =>
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
                    InstrumentSorter.                             Add(entry, cache.Instruments);
                }
            });
        }

        protected override void Deserialize(FileStream stream)
        {
            CategoryCacheStrings strings = new(stream, true);
            var tracker = new ParallelExceptionTracker();
            var entryTasks = new List<Task>();
            var conTasks = new List<Task>();

            try
            {
                AddParallelEntryTasks(stream, ref entryTasks, strings, ReadIniGroup, tracker);
                AddParallelCONTasks(stream, ref conTasks, ReadUpdateDirectory, tracker);
                AddParallelCONTasks(stream, ref conTasks, ReadUpgradeDirectory, tracker);
                AddParallelCONTasks(stream, ref conTasks, ReadUpgradeCON, tracker);
                Task.WaitAll(conTasks.ToArray());

                AddParallelEntryTasks(stream, ref entryTasks, strings, ReadPackedCONGroup, tracker);
                AddParallelEntryTasks(stream, ref entryTasks, strings, ReadUnpackedCONGroup, tracker);
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

        protected override void Deserialize_Quick(FileStream stream)
        {
            YargLogger.LogDebug("Quick Read start");
            CategoryCacheStrings strings = new(stream, true);
            var tracker = new ParallelExceptionTracker();
            var entryTasks = new List<Task>();
            var conTasks = new List<Task>();

            try
            {
                AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadIniGroup, tracker);

                int count = stream.Read<int>(Endianness.Little);
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.Read<int>(Endianness.Little);
                    stream.Position += length;
                }

                AddParallelCONTasks(stream, ref conTasks, QuickReadUpgradeDirectory, tracker);
                AddParallelCONTasks(stream, ref conTasks, QuickReadUpgradeCON, tracker);
                Task.WaitAll(conTasks.ToArray());

                AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadCONGroup, tracker);
                AddParallelEntryTasks(stream, ref entryTasks, strings, QuickReadExtractedCONGroup, tracker);
            }
            catch (Exception ex)
            {
                tracker.Set(ex);
                Task.WaitAll(conTasks.ToArray());
            }
            Task.WaitAll(entryTasks.ToArray());

            if (tracker.IsSet())
            {
                throw tracker;
            }
        }

        protected override void AddUpgrade(string name, YARGDTAReader? reader, IRBProUpgrade upgrade)
        {
            lock (upgrades)
            {
                upgrades[name] = new(reader, upgrade);
            }
        }

        protected override void AddPackedCONGroup(PackedCONGroup group)
        {
            lock (conGroups)
            {
                conGroups.Add(group);
            }
        }

        protected override void AddUnpackedCONGroup(UnpackedCONGroup group)
        {
            lock (extractedConGroups)
            {
                extractedConGroups.Add(group);
            }
        }

        protected override void AddUpgradeGroup(UpgradeGroup group)
        {
            lock (upgradeGroups)
            {
                upgradeGroups.Add(group);
            }
        }

        protected override void RemoveCONEntry(string shortname)
        {
            lock (conGroups)
            {
                foreach (var group in conGroups)
                {
                    if (group.RemoveEntries(shortname))
                    {
                        YargLogger.LogFormatTrace("{0} - {1} pending rescan", group.Location, item2: shortname);
                    }
                }
            }

            lock (extractedConGroups)
            {
                foreach (var group in extractedConGroups)
                {
                    if (group.RemoveEntries(shortname))
                    {
                        YargLogger.LogFormatTrace("{0} - {1} pending rescan", group.Location, item2: shortname);
                    }
                }
            }
        }

        protected override bool CanAddUpgrade(string shortname, DateTime lastUpdated)
        {
            lock (upgradeGroups)
            {
                return CanAddUpgrade(upgradeGroups, shortname, lastUpdated) ?? false;
            }
        }

        protected override bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastUpdated)
        {
            lock (conGroups)
            {
                var result = CanAddUpgrade(conGroups, shortname, lastUpdated);
                if (result != null)
                {
                    return (bool)result;
                }
            }

            lock (upgradeGroups)
            {
                return CanAddUpgrade(upgradeGroups, shortname, lastUpdated) ?? false;
            }
        }

        protected override bool FindOrMarkDirectory(string directory)
        {
            lock (preScannedDirectories)
            {
                return base.FindOrMarkDirectory(directory);
            }
        }

        protected override bool FindOrMarkFile(string file)
        {
            lock (preScannedFiles)
            {
                return base.FindOrMarkFile(file);
            }
        }

        protected override void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badSongs)
            {
                base.AddToBadSongs(filePath, err);
            }
        }

        protected override void AddInvalidSong(string name)
        {
            lock (invalidSongsInCache)
            {
                base.AddInvalidSong(name);
            }
        }

        protected override PackedCONGroup? FindCONGroup(string filename)
        {
            lock (conGroups)
            {
                return conGroups.Find(node => node.Location == filename);
            }
        }

        private void ReadIniGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                return;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        ReadIniEntry(directory, group, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void ReadPackedCONGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadCONGroupHeader(reader);
            if (group != null)
            {
                ReadCONGroup(group, reader, entryTasks, strings, tracker);
            }
        }

        private void ReadUnpackedCONGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadExtractedCONGroupHeader(reader);
            if (group != null)
            {
                ReadCONGroup(group, reader, entryTasks, strings, tracker);
            }
        }

        private void ReadCONGroup<TGroup>(TGroup group, BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
            where TGroup : CONGroup
        {
            ReadCONGroup(reader, (string name, int index, BinaryReader entryReader) =>
            {
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        group.ReadEntry(name, index, upgrades, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            });
        }

        private void QuickReadIniGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        QuickReadIniEntry(directory, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void QuickReadCONGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        AddEntry(PackedRBCONEntry.LoadFromCache_Quick(in group.ConFile, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void QuickReadExtractedCONGroup(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadString();
            var dta = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "songs.dta"), reader, false);
            // Lack of null check of `dta` by design

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        AddEntry(UnpackedRBCONEntry.LoadFromCache_Quick(directory, dta, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
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
    }
}
