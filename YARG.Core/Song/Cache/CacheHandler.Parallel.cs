using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
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

        private void ScanDirectory_Parallel(DirectoryInfo directory, IniGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!TraversalPreTest(directory, tracker.Playlist, CreateUpdateGroup_Parallel))
                    return;

                var collector = new FileCollector(directory);
                if (ScanIniEntry(collector, group, tracker.Playlist))
                {
                    if (collector.subDirectories.Count > 0)
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                    return;
                }

                tracker.Append(directory.FullName);

                var tasks = new Task[collector.subDirectories.Count + collector.subfiles.Count];
                int index = 0;
                foreach (var subDirectory in collector.subDirectories)
                {
                    tasks[index++] = Task.Run(() => ScanDirectory_Parallel(subDirectory, group, tracker));
                }

                foreach (var file in collector.subfiles)
                {
                    tasks[index++] = Task.Run(() => ScanFile(file, group, ref tracker));
                }
                Task.WaitAll(tasks);
                foreach(var task in tasks) task.Dispose();
            }
            catch (PathTooLongException)
            {
                YargTrace.LogError($"Path {directory.FullName} is too long for the file system!");
                AddToBadSongs(directory.FullName, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning directory {directory.FullName}!");
            }
        }

        private void ScanCONGroup_Parallel(PackedCONGroup group)
        {
            var reader = group.LoadSongs();
            if (reader == null)
                return;

            try
            {
                Dictionary<string, int> indices = new();
                List<Task> tasks = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    var node = reader.Clone();
                    tasks.Add(Task.Run(() => ScanPackedCONNode(group, name, index, node)));
                    reader.EndNode();
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning packed CON group {group.Location}!");
            }
            group.CONFile.Dispose();
        }

        private void ScanExtractedCONGroup_Parallel(UnpackedCONGroup group)
        {
            var reader = group.LoadDTA();
            if (reader == null)
                return;

            try
            {
                Dictionary<string, int> indices = new();
                List<Task> tasks = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    var node = reader.Clone();
                    tasks.Add(Task.Run(() => ScanUnpackedCONNode(group, name, index, node)));
                    reader.EndNode();
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning extracted CON group {group.Location}!");
            }
        }

        private void ReadIniGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
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

        private void ReadCONGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadCONGroupHeader(reader, out string filename);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();
                if (invalidSongsInCache.Contains(name))
                {
                    reader.Move(length);
                    continue;
                }

                var entryReader = reader.Slice(length);
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
            }
        }

        private void ReadExtractedCONGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadExtractedCONGroupHeader(reader, out string directory);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.Move(length);
                    continue;
                }

                var entryReader = reader.Slice(length);
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
            }
        }

        private void QuickReadIniGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
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

        private void QuickReadCONGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
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
                        AddEntry(PackedRBCONEntry.LoadFromCache_Quick(group.CONFile, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void QuickReadExtractedCONGroup_Parallel(BinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadString();
            var dta = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "songs.dta"), reader);
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

        private UpdateGroup? CreateUpdateGroup_Parallel(string directory, AbridgedFileInfo dta, bool removeEntries)
        {
            var nodes = FindUpdateNodes(directory, dta);
            if (nodes == null)
            {
                return null;
            }

            var group = new UpdateGroup(directory, dta.LastUpdatedTime);
            Parallel.ForEach(nodes, node => ScanUpdateNode(group, node.Key, node.Value.ToArray(), removeEntries));
            updateGroups.Add(group);
            return group;
        }
    }
}
