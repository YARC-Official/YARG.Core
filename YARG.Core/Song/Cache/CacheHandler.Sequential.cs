using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private void ScanDirectory(DirectoryInfo directory, IniGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!TraversalPreTest(directory, tracker.Playlist))
                    return;

                var collector = new FileCollector(directory);
                if (ScanIniEntry(collector, group, tracker.Playlist))
                    return;

                tracker.Append(directory.FullName);
                foreach (var subDirectory in collector.subDirectories)
                {
                    ScanDirectory(subDirectory, group, tracker);
                }

                foreach (var file in collector.subfiles)
                {
                    ScanFile(file, group, ref tracker);
                }
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

        private void ScanCONGroup(PackedCONGroup group)
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

                    ScanPackedCONNode(group, name, index, reader);
                    reader.EndNode();
                }
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning packed CON group {group.Location}!");
            }
            group.CONFile.Dispose();
        }

        private void ScanExtractedCONGroup(UnpackedCONGroup group)
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

                    ScanUnpackedCONNode(group, name, index, reader);
                    reader.EndNode();
                }
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning extracted CON group {group.Location}!");
            }
        }

        private void ReadIniGroup(YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
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
                group.ReadEntry(name, index, upgrades, entryReader, strings);
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
                group.ReadEntry(name, index, upgrades, entryReader, strings);
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
