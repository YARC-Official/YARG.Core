using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private void ScanDirectory(DirectoryInfo directory, IniGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!TraversalPreTest(directory, tracker.Playlist, CreateUpdateGroup))
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

        private void ReadIniGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            var group = GetBaseIniGroup(directory);
            if (group == null)
            {
                return;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                ReadIniEntry(directory, group, entryReader, strings);
            }
        }

        private void ReadCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader, out string filename);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
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
                group.ReadEntry(name, index, upgrades, entryReader, strings);
            }
        }

        private void ReadExtractedCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(reader, out string directory);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
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
                group.ReadEntry(name, index, upgrades, entryReader, strings);
            }
        }

        private void QuickReadIniGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                QuickReadIniEntry(directory, entryReader, strings);
            }
        }

        private void QuickReadCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                AddEntry(PackedRBCONEntry.LoadFromCache_Quick(group.CONFile, name, upgrades, entryReader, strings));
            }
        }

        private void QuickReadExtractedCONGroup(BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadString();
            var dta = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "songs.dta"), reader);
            // Lack of null check of dta by design

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadString();
                // index
                reader.Move(4);

                int length = reader.ReadInt32();
                var entryReader = reader.Slice(length);
                AddEntry(UnpackedRBCONEntry.LoadFromCache_Quick(directory, dta, name, upgrades, entryReader, strings));
            }
        }

        private UpdateGroup? CreateUpdateGroup(string directory, AbridgedFileInfo dta, bool removeEntries)
        {
            var nodes = FindUpdateNodes(directory, dta);
            if (nodes == null)
            {
                return null;
            }

            var group = new UpdateGroup(directory, dta.LastUpdatedTime);
            foreach (var node in nodes)
            {
                ScanUpdateNode(group, node.Key, node.Value.ToArray(), removeEntries);
            }
            updateGroups.Add(group);
            return group;
        }
    }
}
