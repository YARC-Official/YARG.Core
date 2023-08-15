using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private void ScanDirectory(string directory, int index)
        {
            try
            {
                if (!TraversalPreTest(directory))
                    return;

                var result = FileCollector.Collect(directory);
                if (ScanIniEntry(result, index))
                    return;

                foreach (string file in result.subfiles)
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.Directory) != 0)
                            ScanDirectory(file, index);
                        else
                            AddPossibleCON(file);
                    }
                    catch (PathTooLongException)
                    {
                        YargTrace.LogWarning($"Path {file} is too long for Windows OS");
                    }
                    catch (Exception e)
                    {
                        AddErrors(file + ": " + e.Message);
                    }
                }
            }
            catch (PathTooLongException)
            {
                YargTrace.LogWarning($"Path {directory} is too long for Windows OS");
            }
            catch (Exception e)
            {
                AddErrors(directory + ": " + e.Message);
            }
        }

        private void ReadIniGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            int baseIndex = GetBaseDirectoryIndex(directory);
            if (baseIndex == -1)
            {
                YargTrace.DebugInfo($"Ini group outside base directories : {directory}");
                return;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                ReadIniEntry(directory, baseIndex, new(reader, length), strings);
            }
        }

        private void ReadCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader);
            if (group == null)
                return;

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

                if (!group.ReadEntry(name, index, upgrades, new(reader, length), strings))
                    YargTrace.DebugInfo($"CON entry invalid {group.file.filename} | {name}");
            }
        }

        private void ReadExtractedCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
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

                if (!group.ReadEntry(name, index, upgrades, new(reader, length), strings))
                    YargTrace.DebugInfo($"EXCON entry invalid {group.directory} | {name}");
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                QuickReadIniEntry(directory, new(reader, length), strings);
            }
        }

        private void QuickReadCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Position += 4;

                int length = reader.ReadInt32();
                AddEntry(SongMetadata.PackedRBCONFromCache_Quick(group.file, name, upgrades, new(reader, length), strings));
            }
        }

        private void QuickReadExtractedCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Position += 4;

                int length = reader.ReadInt32();
                AddEntry(SongMetadata.UnpackedRBCONFromCache_Quick(dta, name, upgrades, new(reader, length), strings));
            }
        }
    }
}
