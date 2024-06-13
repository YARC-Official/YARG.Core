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
            var tracker = new PlaylistTracker(fullDirectoryPlaylists, null);
            Parallel.ForEach(iniGroups, group =>
            {
                var dirInfo = new DirectoryInfo(group.Directory);
                ScanDirectory(dirInfo, group, tracker);
            });

            var conActions = new Action[conGroups.Count + extractedConGroups.Count];
            for (int i = 0; i < conGroups.Count; ++i)
            {
                var group = conGroups[i];
                conActions[i] = () =>
                {
                    if (group.LoadSongs(out var reader))
                    {
                        ScanCONGroup(group, ref reader, ScanPackedCONNode);
                    }
                    group.DisposeStreamAndSongDTA();
                };
            }

            for (int i = 0; i < extractedConGroups.Count; ++i)
            {
                var group = extractedConGroups[i];
                conActions[conGroups.Count + i] = () =>
                {
                    if (group.LoadDTA(out var reader))
                    {
                        ScanCONGroup(group, ref reader, ScanUnpackedCONNode);
                    }
                    group.Dispose();
                };
            }

            Parallel.ForEach(conActions, action => action());
            foreach (var group in conGroups)
            {
                group.DisposeUpgradeDTA();
            }
        }

        protected override void TraverseDirectory(in FileCollection collection, IniGroup group, PlaylistTracker tracker)
        {
            var actions = new Action[collection.subDirectories.Count + collection.subfiles.Count];
            int index = 0;
            foreach (var directory in collection.subDirectories)
            {
                actions[index++] = () => ScanDirectory(directory.Value, group, tracker);
            }

            foreach (var file in collection.subfiles)
            {
                actions[index++] = () => ScanFile(file.Value, group, in tracker);
            }
            Parallel.ForEach(actions, action => action());
        }

        private void ScanCONGroup<TGroup>(TGroup group, ref YARGDTAReader reader, Action<TGroup, string, int, YARGDTAReader> func)
            where TGroup : CONGroup
        {
            try
            {
                var slices = new List<(string Name, int Index, YARGDTAReader Reader)>();
                Dictionary<string, int> indices = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode(true);
                    if (indices.TryGetValue(name, out int index))
                    {
                        ++index;
                    }
                    indices[name] = index;

                    slices.Add((name, index, reader));
                    reader.EndNode();
                }
                Parallel.ForEach(slices, slice => func(group, slice.Name, slice.Index, slice.Reader));
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning CON group {group.Location}!");
            }
        }

        protected override bool AddEntry(SongEntry entry)
        {
            lock (cache.Entries)
            {
                return base.AddEntry(entry);
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

        protected override void Deserialize(UnmanagedMemoryStream stream)
        {
            CategoryCacheStrings strings = new(stream, true);
            var tracker = new ParallelExceptionTracker();
            var entryTasks = new List<Task>();
            var conTasks = new List<Task>();

            try
            {
                AddParallelEntryTasks(stream, entryTasks, strings, ReadIniGroup, tracker);
                AddParallelCONTasks(stream, conTasks, ReadUpdateDirectory, tracker);
                AddParallelCONTasks(stream, conTasks, ReadUpgradeDirectory, tracker);
                AddParallelCONTasks(stream, conTasks, ReadUpgradeCON, tracker);
                Task.WaitAll(conTasks.ToArray());

                AddParallelEntryTasks(stream, entryTasks, strings, ReadPackedCONGroup, tracker);
                AddParallelEntryTasks(stream, entryTasks, strings, ReadUnpackedCONGroup, tracker);
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

        protected override void Deserialize_Quick(UnmanagedMemoryStream stream)
        {
            YargLogger.LogDebug("Quick Read start");
            CategoryCacheStrings strings = new(stream, true);
            var tracker = new ParallelExceptionTracker();
            var entryTasks = new List<Task>();
            var conTasks = new List<Task>();

            try
            {
                AddParallelEntryTasks(stream, entryTasks, strings, QuickReadIniGroup, tracker);

                int skipLength = stream.Read<int>(Endianness.Little);
                stream.Position += skipLength;

                AddParallelCONTasks(stream, conTasks, QuickReadUpgradeDirectory, tracker);
                AddParallelCONTasks(stream, conTasks, QuickReadUpgradeCON, tracker);
                Task.WaitAll(conTasks.ToArray());

                AddParallelEntryTasks(stream, entryTasks, strings, QuickReadCONGroup, tracker);
                AddParallelEntryTasks(stream, entryTasks, strings, QuickReadExtractedCONGroup, tracker);
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

        protected override void AddUpdate(string name, DateTime dtaLastWrite, in SongUpdate update)
        {
            lock (updates)
            {
                if (!updates.TryGetValue(name, out var list))
                {
                    updates.Add(name, list = new());
                }
                list.Add(dtaLastWrite, update);
            }
        }

        protected override void AddUpgrade(string name, YARGDTAReader reader, IRBProUpgrade upgrade)
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

        protected override void AddUpdateGroup(UpdateGroup group)
        {
            lock (updateGroups)
            {
                updateGroups.Add(group);
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

        private readonly struct CacheEnumerable<T> : IEnumerable<T>
        {
            private readonly UnmanagedMemoryStream _stream;
            private readonly ParallelExceptionTracker _tracker;
            private readonly Func<T?> _creator;

            public CacheEnumerable(UnmanagedMemoryStream stream, ParallelExceptionTracker tracker, Func<T?> creator)
            {
                _stream = stream;
                _tracker = tracker;
                _creator = creator;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private struct Enumerator : IEnumerator<T>, IEnumerator
            {
                private readonly ParallelExceptionTracker _tracker;
                private readonly Func<T?> _creator;

                private readonly int _count;
                private int _index;
                private T _current;

                public Enumerator(CacheEnumerable<T> values)
                {
                    _tracker = values._tracker;
                    _creator = values._creator;
                    _count = values._stream.Read<int>(Endianness.Little);
                    _index = 0;
                    _current = default!;
                }

                public readonly T Current => _current;

                readonly object IEnumerator.Current => _current!;

                public void Dispose()
                {
                    _current = default!;
                }

                public bool MoveNext()
                {
                    while (_index < _count && !_tracker.IsSet())
                    {
                        ++_index;
                        var slice = _creator();
                        if (slice != null)
                        {
                            _current = slice;
                            return true;
                        }
                    }
                    return false;
                }

                public void Reset()
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void ReadIniGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = stream.ReadString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                return;
            }

            var enumerable = new CacheEnumerable<UnmanagedMemoryStream>(stream, tracker, () =>
            {
                int length = stream.Read<int>(Endianness.Little);
                return stream.Slice(length);
            });

            Parallel.ForEach(enumerable, slice =>
            {
                try
                {
                    ReadIniEntry(directory, group, slice, strings);
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                }
            });
        }

        private void ReadPackedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadCONGroupHeader(stream);
            if (group != null)
            {
                ReadCONGroup(group, stream, strings, tracker);
            }
        }

        private void ReadUnpackedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadExtractedCONGroupHeader(stream);
            if (group != null)
            {
                ReadCONGroup(group, stream, strings, tracker);
            }
        }

        private void ReadCONGroup<TGroup>(TGroup group, UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
            where TGroup : CONGroup
        {
            var enumerable = new CacheEnumerable<(string Name, int Index, UnmanagedMemoryStream Stream)?>(stream, tracker, () =>
            {
                string name = stream.ReadString();
                int index = stream.Read<int>(Endianness.Little);
                int length = stream.Read<int>(Endianness.Little);
                if (invalidSongsInCache.Contains(name))
                {
                    stream.Position += length;
                    return null;
                }
                return (name, index, stream.Slice(length));
            });

            Parallel.ForEach(enumerable, slice =>
            {
                var value = slice!.Value;
                // Error catching must be done per-thread
                try
                {
                    group.ReadEntry(value.Name, value.Index, upgrades, value.Stream, strings);
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                }
            });
        }

        private void QuickReadIniGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = stream.ReadString();
            var enumerable = new CacheEnumerable<UnmanagedMemoryStream>(stream, tracker, () =>
            {
                int length = stream.Read<int>(Endianness.Little);
                return stream.Slice(length);
            });

            Parallel.ForEach(enumerable, slice =>
            {
                try
                {
                    QuickReadIniEntry(directory, slice, strings);
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                }
            });
        }

        private void QuickReadCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = QuickReadCONGroupHeader(stream);
            if (group == null)
                return;

            var enumerable = new CacheEnumerable<(string Name, UnmanagedMemoryStream Stream)>(stream, tracker, () =>
            {
                string name = stream.ReadString();
                // index
                stream.Position += 4;

                int length = stream.Read<int>(Endianness.Little);
                return (name, stream.Slice(length));
            });

            Parallel.ForEach(enumerable, slice =>
            {
                try
                {
                    AddEntry(PackedRBCONEntry.LoadFromCache_Quick(in group.ConFile, slice.Name, upgrades, slice.Stream, strings));
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                }
            });
        }

        private void QuickReadExtractedCONGroup(UnmanagedMemoryStream stream, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = stream.ReadString();
            var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            var dta = new AbridgedFileInfo_Length(Path.Combine(directory, "songs.dta"), lastWrite, 0);

            var enumerable = new CacheEnumerable<(string Name, UnmanagedMemoryStream Stream)>(stream, tracker, () =>
            {
                string name = stream.ReadString();
                // index
                stream.Position += 4;

                int length = stream.Read<int>(Endianness.Little);
                return (name, stream.Slice(length));
            });

            Parallel.ForEach(enumerable, slice =>
            {
                try
                {
                    AddEntry(UnpackedRBCONEntry.LoadFromCache_Quick(directory, dta, slice.Name, upgrades, slice.Stream, strings));
                }
                catch (Exception ex)
                {
                    tracker.Set(ex);
                }
            });
        }

        private static void AddParallelCONTasks(UnmanagedMemoryStream stream, List<Task> conTasks, Action<UnmanagedMemoryStream> func, ParallelExceptionTracker tracker)
        {
            int sectionLength = stream.Read<int>(Endianness.Little);
            var sectionSlice = stream.Slice(sectionLength);
            var enumerable = new CacheEnumerable<UnmanagedMemoryStream>(sectionSlice, tracker, () =>
            {
                int length = sectionSlice.Read<int>(Endianness.Little);
                return sectionSlice.Slice(length);
            });

            conTasks.Add(Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(enumerable, groupSlice =>
                {
                    try
                    {
                        func(groupSlice);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                });
            }, TaskCreationOptions.LongRunning));
        }

        private static void AddParallelEntryTasks(UnmanagedMemoryStream stream, List<Task> entryTasks, CategoryCacheStrings strings, Action<UnmanagedMemoryStream, CategoryCacheStrings, ParallelExceptionTracker> func, ParallelExceptionTracker tracker)
        {
            int sectionLength = stream.Read<int>(Endianness.Little);
            var sectionSlice = stream.Slice(sectionLength);
            var enumerable = new CacheEnumerable<UnmanagedMemoryStream>(sectionSlice, tracker, () =>
            {
                int length = sectionSlice.Read<int>(Endianness.Little);
                return sectionSlice.Slice(length);
            });

            entryTasks.Add(Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(enumerable, groupSlice =>
                {
                    try
                    {
                        func(groupSlice, strings, tracker);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                });
            }, TaskCreationOptions.LongRunning));
        }
    }
}
