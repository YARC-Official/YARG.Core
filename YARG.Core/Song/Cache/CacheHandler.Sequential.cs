using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private void ScanDirectory(string directory, IniGroup group)
        {
            try
            {
                if (!TraversalPreTest(directory))
                    return;

                var result = new FileCollector(directory);
                if (ScanIniEntry(result, group))
                    return;

                foreach (string file in result.subfiles)
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);

                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            ScanDirectory(file, group);
                        }
                        else if (file.EndsWith(".sng"))
                        {
                            ScanSngFile(false, file, group);
                        }
                        else if (file.EndsWith(".yargsong"))
                        {
                            ScanSngFile(true, file, group);
                        }
                        else
                        {
                            AddPossibleCON(file);
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
                }
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

        private void ScanCONGroup(string filename, PackedCONGroup group)
        {
            var reader = group.LoadSongs();
            if (reader == null)
                return;

            try
            {
                Dictionary<string, int> indices = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    ScanPackedCONNode(filename, group, name, index, reader);
                    reader.EndNode();
                }
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning packed CON group {filename}!");
            }
            group.CONFile.Dispose();
        }

        private void ScanExtractedCONGroup(string directory, UnpackedCONGroup group)
        {
            var reader = group.LoadDTA();
            if (reader == null)
                return;

            try
            {
                Dictionary<string, int> indices = new();
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index = GetCONIndex(indices, name);

                    ScanUnpackedCONNode(directory, group, name, index, reader);
                    reader.EndNode();
                }
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning extracted CON group {directory}!");
            }
        }

        private void ReadIniGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                YargTrace.DebugInfo($"Ini group outside base directories : {directory}");
                return;
            }

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
                ReadIniEntry(directory, group, entryReader, strings);
            }
        }

        private void ReadCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader, out string filename);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
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
                if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                    YargTrace.DebugInfo($"CON entry {name} in group {filename} is invalid!");
            }
        }

        private void ReadExtractedCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(reader, out string directory);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
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
                if (!group.ReadEntry(name, index, upgrades, entryReader, strings))
                    YargTrace.DebugInfo($"Extracted CON entry {name} in group {directory} is invalid!");
            }
        }

        private void QuickReadIniGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
                QuickReadIniEntry(directory, entryReader, strings);
            }
        }

        private void QuickReadCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Move(4);

                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
                AddEntry(SongMetadata.PackedRBCONFromCache_Quick(group.CONFile, name, upgrades, entryReader, strings));
            }
        }

        private void QuickReadExtractedCONGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);
            // Lack of null check by design

            int count = reader.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                // index
                reader.Move(4);

                int length = reader.Read<int>(Endianness.Little);
                var entryReader = reader.Slice(length);
                AddEntry(SongMetadata.UnpackedRBCONFromCache_Quick(dta, name, upgrades, entryReader, strings));
            }
        }
    }
}
