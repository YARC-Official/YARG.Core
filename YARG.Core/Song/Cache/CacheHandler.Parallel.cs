using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
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
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.Directory) != 0)
                        ScanDirectory_Parallel(file, index);
                    else
                        AddPossibleCON(file);
                });
            }
            catch (Exception e)
            {
                AddErrors(directory + ": " + e.Message);
            }
        }

        private void ScanCONGroup_Parallel(PackedCONGroup group)
        {
            if (!group.LoadSongs(out var reader))
                return;

            try
            {
                Dictionary<string, int> indices = new();
                object indexLock = new();
                List<Task> tasks = new();
                while (reader!.StartNode())
                {
                    var node = new YARGDTAReader(reader);
                    tasks.Add(Task.Run(() =>
                    {
                        string name = node.GetNameOfNode();
                        int index;
                        lock (indexLock)
                        {
                            if (indices.ContainsKey(name))
                                index = ++indices[name];
                            else
                                index = indices[name] = 0;
                        }

                        if (group.TryGetEntry(name, index, out var entry))
                        {
                            if (!AddEntry(entry!))
                                group.RemoveEntry(name, index);
                        }
                        else
                        {
                            var song = SongMetadata.FromPackedRBCON(group.file, name, node, updates, upgrades);
                            if (song.Item2 != null)
                            {
                                if (AddEntry(song.Item2))
                                    group.AddEntry(name, index, song.Item2);
                            }
                            else
                            {
                                AddToBadSongs(group.file.filename + $" - Node {name}", song.Item1);
                            }
                        }
                    }));
                    reader.EndNode();
                }

                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                AddErrors(e);
            }
        }

        private void ScanExtractedCONGroup_Parallel(UnpackedCONGroup group)
        {
            try
            {
                YARGDTAReader reader = new(group.dta.FullName);
                Dictionary<string, int> indices = new();
                object indexLock = new();
                List<Task> tasks = new();
                while (reader.StartNode())
                {
                    var node = new YARGDTAReader(reader);
                    tasks.Add(Task.Run(() =>
                    {
                        string name = node.GetNameOfNode();
                        int index;
                        lock (indexLock)
                        {
                            if (indices.ContainsKey(name))
                                index = ++indices[name];
                            else
                                index = indices[name] = 0;
                        }

                        if (group.TryGetEntry(name, index, out var entry))
                        {
                            if (!AddEntry(entry!))
                                group.RemoveEntry(name, index);
                        }
                        else
                        {
                            var song = SongMetadata.FromUnpackedRBCON(group.directory, group.dta, name, node, updates, upgrades);
                            if (song.Item2 != null)
                            {
                                if (AddEntry(song.Item2))
                                    group.AddEntry(name, index, song.Item2);
                            }
                            else
                            {
                                AddToBadSongs(group.directory + $" - Node {name}", song.Item1);
                            }
                        }
                    }));
                    reader.EndNode();
                }
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception e)
            {
                AddErrors(e);
            }
        }

        private void ReadIniGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            int baseIndex = GetBaseDirectoryIndex(directory);
            if (baseIndex == -1)
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() => 
                { 
                    try 
                    {
                        ReadIniEntry(directory, baseIndex, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }
            Task.WaitAll(entryTasks.ToArray());
        }

        private void ReadCONGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader);
            if (group == null)
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
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
                    try
                    {
                        group.ReadEntry(name, index, upgrades, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void ReadExtractedCONGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
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
                    try
                    {
                        group.ReadEntry(name, index, upgrades, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (GetBaseDirectoryIndex(directory) == -1)
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        QuickReadIniEntry(directory, entryReader, strings);
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }
            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadCONGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;

                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        AddEntry(SongMetadata.PackedRBCONFromCache_Quick(group.file, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadExtractedCONGroup_Parallel(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);
            if (dta == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;

                int length = reader.ReadInt32();
                var entryReader = new YARGBinaryReader(reader, length);
                entryTasks.Add(Task.Run(() => {
                    try
                    {
                        AddEntry(SongMetadata.UnpackedRBCONFromCache_Quick(dta, name, upgrades, entryReader, strings));
                    }
                    catch (Exception ex)
                    {
                        AddErrors(ex);
                    }
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }
    }
}
