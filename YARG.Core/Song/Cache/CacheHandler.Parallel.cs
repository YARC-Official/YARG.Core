using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private sealed class ParallelExceptionTracker
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
        }

        private void ScanDirectory_Parallel(string directory, int index)
        {
            try
            {
                if (!TraversalPreTest(directory))
                    return;

                var result = FileCollector.Collect(directory);
                if (ScanIniEntry(result, index))
                    return;

                Parallel.ForEach(result.subfiles, file =>
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.Directory) != 0)
                            ScanDirectory_Parallel(file, index);
                        else
                            AddPossibleCON(file);
                    }
                    catch (PathTooLongException)
                    {
                        YargTrace.LogWarning($"Path {file} is too long for the file system!");
                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error while scanning file {file}!");
                    }
                });
            }
            catch (PathTooLongException)
            {
                YargTrace.LogWarning($"Path {directory} is too long for the file system!");
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning directory {directory}!");
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
                while (reader!.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    var node = new YARGDTAReader(reader);
                    tasks.Add(Task.Run(() => ScanPackedCONNode(group, name, index, node)));
                    reader.EndNode();
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning packed CON group {group.file.filename}!");
            }
        }

        private void ScanExtractedCONGroup_Parallel(UnpackedCONGroup group)
        {
            try
            {
                YARGDTAReader reader = new(group.dta.FullName);
                Dictionary<string, int> indices = new();
                List<Task> tasks = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    var node = new YARGDTAReader(reader);
                    tasks.Add(Task.Run(() => ScanUnpackedCONNode(group, name, index, node)));
                    reader.EndNode();
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning extracted CON group {group.directory}!");
            }
        }

        private void ReadIniGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadLEBString();
            int baseIndex = GetBaseDirectoryIndex(directory);
            if (baseIndex == -1)
            {
                YargTrace.DebugInfo($"INI group outside base directories: {directory}");
                return;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        ReadIniEntry(directory, baseIndex, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void ReadCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();
                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                            YargTrace.DebugError($"CON entry {name} in group {group.file.filename} is invalid!");
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void ReadExtractedCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadExtractedCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                            YargTrace.DebugError($"Extracted CON entry {name} in group {group.directory} is invalid!");
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void QuickReadIniGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadLEBString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
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

        private void QuickReadCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Position += 4;

                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        AddEntry(SongMetadata.PackedRBCONFromCache_Quick(group.file, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }

        private void QuickReadExtractedCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);

            int count = reader.ReadInt32();
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Position += 4;

                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        AddEntry(SongMetadata.UnpackedRBCONFromCache_Quick(dta, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        tracker.Set(ex);
                    }
                }));
            }
        }
    }
}
