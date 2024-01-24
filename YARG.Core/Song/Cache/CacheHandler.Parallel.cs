using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
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

        private void ScanDirectory_Parallel(string directory, IniGroup group)
        {
            try
            {
                if (!TraversalPreTest(directory))
                    return;

                var result = new FileCollector(directory);
                if (ScanIniEntry(result, group))
                    return;

                Parallel.ForEach(result.subfiles, file =>
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            ScanDirectory_Parallel(file, group);
                        }
                        else if (FindOrMarkFile(file))
                        {
                            if (!AddPossibleCON(file) && (file.EndsWith(".sng") || file.EndsWith(".yargsong")))
                            {
                                ScanSngFile(file, group);
                            }
                        }
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
                while (reader.StartNode())
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

                    var node = new YARGDTAReader(reader);
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

        private void ReadIniGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            string directory = reader.ReadLEBString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                YargTrace.DebugInfo($"INI group outside base directories: {directory}");
                return;
            }

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.Read<int>(Endianness.Little);
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

        private void ReadCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = ReadCONGroupHeader(reader, out string filename);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.Read<int>(Endianness.Little);
                int length = reader.Read<int>(Endianness.Little);
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
                        if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                            YargTrace.DebugInfo($"CON entry {name} in group {filename} is invalid!");
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
            var group = ReadExtractedCONGroupHeader(reader, out string directory);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.Read<int>(Endianness.Little);
                int length = reader.Read<int>(Endianness.Little);

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
                        if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                            YargTrace.DebugInfo($"Extracted CON entry {name} in group {directory} is invalid!");
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
            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                int length = reader.Read<int>(Endianness.Little);
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

        private void QuickReadCONGroup_Parallel(YARGBinaryReader reader, List<Task> entryTasks, CategoryCacheStrings strings, ParallelExceptionTracker tracker)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Move(4);

                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
                entryTasks.Add(Task.Run(() =>
                {
                    // Error catching must be done per-thread
                    try
                    {
                        AddEntry(SongMetadata.PackedRBCONFromCache_Quick(group.CONFile, name, upgrades, entryReader, strings));
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

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count && !tracker.IsSet(); ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Move(4);

                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
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
