using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private readonly HashSet<string> invalidSongsInCache = new();

        private static void RunTasks(FileStream stream, Action<YARGBinaryReader> func)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                func(reader);
            }
        }

        private static void RunTasks(FileStream stream, CategoryCacheStrings strings, Action<YARGBinaryReader, CategoryCacheStrings> func)
        {
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                func(reader, strings);
            }
        }

        private static List<Task> CreateParallelTasks(FileStream stream, Action<YARGBinaryReader> func)
        {
            List<Task> tasks = new();
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                tasks.Add(Task.Run(() => func(reader)));
            }
            return tasks;
        }

        private static List<Task> CreateParallelTasks(FileStream stream, CategoryCacheStrings strings, Action<YARGBinaryReader, CategoryCacheStrings> func)
        {
            List<Task> tasks = new();
            int count = stream.ReadInt32LE();
            for (int i = 0; i < count; ++i)
            {
                int length = stream.ReadInt32LE();
                YARGBinaryReader reader = new(stream.ReadBytes(length));
                tasks.Add(Task.Run(() => func(reader, strings)));
            }
            return tasks;
        }

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        /// 
        /// </summary>
        private const int MIN_CACHEFILESIZE = 92;

        private FileStream? CheckCacheFile()
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
                return null;

            FileStream fs = new(cacheLocation, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CACHE_VERSION)
            {
                fs.Dispose();
                return null;
            }
            return fs;
        }

        private bool Deserialize()
        {
            Progress = ScanProgress.LoadingCache;
            using var stream = CheckCacheFile();
            if (stream == null)
                return false;

            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                var entryTasks = CreateParallelTasks(stream, strings, ReadIniGroup_Parallel);

                var conTasks = CreateParallelTasks(stream, ReadUpdateDirectory);
                conTasks.AddRange(CreateParallelTasks(stream, ReadUpgradeDirectory));
                conTasks.AddRange(CreateParallelTasks(stream, ReadUpgradeCON));
                Task.WaitAll(conTasks.ToArray());

                entryTasks.AddRange(CreateParallelTasks(stream, strings, ReadCONGroup_Parallel));
                entryTasks.AddRange(CreateParallelTasks(stream, strings, ReadExtractedCONGroup_Parallel));

                Task.WaitAll(entryTasks.ToArray());
            }
            else
            {
                RunTasks(stream, strings, ReadIniGroup);
                RunTasks(stream, ReadUpdateDirectory);
                RunTasks(stream, ReadUpgradeDirectory);
                RunTasks(stream, ReadUpgradeCON);
                RunTasks(stream, strings, ReadCONGroup);
                RunTasks(stream, strings, ReadExtractedCONGroup);
            }
            return true;
        }

        private bool Deserialize_Quick()
        {
            Progress = ScanProgress.LoadingCache;
            var stream = CheckCacheFile();
            if (stream == null)
                return false;

            CategoryCacheStrings strings = new(stream, multithreading);
            if (multithreading)
            {
                var entryTasks = CreateParallelTasks(stream, strings, QuickReadIniGroup_Parallel);

                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    stream!.Position += length;
                }

                var conTasks = CreateParallelTasks(stream, QuickReadUpgradeDirectory);
                conTasks.AddRange(CreateParallelTasks(stream, QuickReadUpgradeCON));
                Task.WaitAll(conTasks.ToArray());

                entryTasks.AddRange(CreateParallelTasks(stream, strings, QuickReadCONGroup_Parallel));
                entryTasks.AddRange(CreateParallelTasks(stream, strings, QuickReadExtractedCONGroup_Parallel));

                Task.WaitAll(entryTasks.ToArray());
            }
            else
            {
                RunTasks(stream, strings, QuickReadIniGroup);

                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    stream!.Position += length;
                }

                RunTasks(stream, QuickReadUpgradeDirectory);
                RunTasks(stream, QuickReadUpgradeCON);
                RunTasks(stream, strings, QuickReadCONGroup);
                RunTasks(stream, strings, QuickReadExtractedCONGroup);
            }
            return true;
        }

        private void Serialize()
        {
            Progress = ScanProgress.WritingCache;
            using var writer = new BinaryWriter(new FileStream(cacheLocation, FileMode.Create, FileAccess.Write));
            Dictionary<SongMetadata, CategoryCacheWriteNode> nodes = new();

            writer.Write(CACHE_VERSION);

            cache.titles.WriteToCache(writer, ref nodes);
            cache.artists.WriteToCache(writer, ref nodes);
            cache.albums.WriteToCache(writer, ref nodes);
            cache.genres.WriteToCache(writer, ref nodes);
            cache.years.WriteToCache(writer, ref nodes);
            cache.charters.WriteToCache(writer, ref nodes);
            cache.playlists.WriteToCache(writer, ref nodes);
            cache.sources.WriteToCache(writer, ref nodes);

            List<PackedCONGroup> upgradeCons = new();
            List<PackedCONGroup> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.UpgradeCount > 0)
                    upgradeCons.Add(group);

                if (group.EntryCount > 0)
                    entryCons.Add(group);
            }

            ICacheGroup.SerializeGroups(iniGroups, writer, nodes);
            IModificationGroup.SerializeGroups(updateGroups, writer);
            IModificationGroup.SerializeGroups(upgradeGroups, writer);
            IModificationGroup.SerializeGroups(upgradeCons, writer);
            ICacheGroup.SerializeGroups(entryCons, writer, nodes);
            ICacheGroup.SerializeGroups(extractedConGroups, writer, nodes);
        }

        private void ReadIniEntry(string baseDirectory, int baseIndex, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = SongMetadata.IniFromCache(baseDirectory, reader, strings);
            if (entry == null)
                return;

            MarkDirectory(entry.Directory);
            AddEntry(entry);
            AddIniEntry(entry, baseIndex);
        }

        private void ReadUpdateDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            if (GetBaseDirectoryIndex(directory) >= 0)
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    CreateUpdateGroup(directory, dta);

                    if (dta.LastWriteTime == dtaLastWrite)
                        return;
                }
            }

            for (int i = 0; i < count; i++)
                AddInvalidSong(reader.ReadLEBString());
        }

        private void ReadUpgradeDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            if (GetBaseDirectoryIndex(directory) >= 0)
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    var group = CreateUpgradeGroup(directory, dta);

                    if (group != null && dta.LastWriteTime == dtaLastWrite)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadLEBString();
                            var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.upgrades.TryGetValue(name, out var upgrade) || upgrade!.LastWrite != lastWrite)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadLEBString());
                reader.Position += 8;
            }
        }

        private void ReadUpgradeCON(YARGBinaryReader cacheReader)
        {
            string filename = cacheReader.ReadLEBString();
            var conLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            var dtaLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int count = cacheReader.ReadInt32();

            if (GetBaseDirectoryIndex(filename) >= 0 && CreateCONGroup(filename, out var group))
            {
                AddCONGroup(group!);
                if (group!.LoadUpgrades(out var reader))
                {
                    AddCONUpgrades(group, reader!);

                    if (group.UpgradeDTALastWrite == dtaLastWrite)
                    {
                        if (group.lastWrite != conLastWrite)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                string name = cacheReader.ReadLEBString();
                                var lastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
                                if (group.upgrades[name].LastWrite != lastWrite)
                                    AddInvalidSong(name);
                            }
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadLEBString());
                cacheReader.Position += 8;
            }
        }

        private PackedCONGroup? ReadCONGroupHeader(YARGBinaryReader reader)
        {
            string filename = reader.ReadLEBString();
            if (GetBaseDirectoryIndex(filename) == -1)
                return null;

            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                FileInfo info = new(filename);
                if (!info.Exists)
                    return null;

                MarkFile(filename);

                var file = CONFile.LoadCON(info.FullName);
                if (file == null)
                    return null;

                group = new(file, info.LastWriteTime);
                AddCONGroup(group);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
                return null;
            return group;
        }

        private UnpackedCONGroup? ReadExtractedCONGroupHeader(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            if (GetBaseDirectoryIndex(directory) == -1)
                return null;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
                return null;

            UnpackedCONGroup group = new(directory, dtaInfo);
            MarkDirectory(directory);
            AddExtractedCONGroup(group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return group;
        }

        private void QuickReadIniEntry(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = SongMetadata.IniFromCache_Quick(baseDirectory, reader, strings);
            if (entry != null)
                AddEntry(entry);
        }

        private void QuickReadUpgradeDirectory(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            UpgradeGroup group = new(directory, dtaLastWrite);
            AddUpgradeGroup(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadLEBString();
                var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                IRBProUpgrade upgrade = new UnpackedRBProUpgrade(filename, lastWrite);
                group.upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        private void QuickReadUpgradeCON(YARGBinaryReader reader)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 12;
            int count = reader.ReadInt32();

            if (CreateCONGroup(filename, out var group))
            {
                var file = group!.file;
                AddCONGroup(group);

                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    var listing = file.TryGetListing($"songs_upgrades/{name}_plus.mid");

                    IRBProUpgrade upgrade = new PackedRBProUpgrade(file, listing, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());

                    IRBProUpgrade upgrade = new PackedRBProUpgrade(null, null, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
        }

        private PackedCONGroup? QuickReadCONGroupHeader(YARGBinaryReader reader)
        {
            string filename = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                if (!CreateCONGroup(filename, out group))
                    return null;
                AddCONGroup(group!);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
                return null;
            return group;
        }

        private AbridgedFileInfo? QuickReadExtractedCONGroupHeader(YARGBinaryReader reader)
        {
            string directory = reader.ReadLEBString();
            if (GetBaseDirectoryIndex(directory) == -1)
                return null;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists || dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return new(dtaInfo);
        }

        private bool CreateCONGroup(string filename, out PackedCONGroup? group)
        {
            group = null;

            FileInfo info = new(filename);
            if (!info.Exists)
                return false;

            MarkFile(filename);

            var file = CONFile.LoadCON(filename);
            if (file == null)
                return false;

            group = new(file, info.LastWriteTime);
            return true;
        }

        private bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conLock)
            {
                foreach (var con in conGroups)
                {
                    if (con.file.filename == filename)
                    {
                        group = con;
                        return true;
                    }
                }
            }
            group = null;
            return false;
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
        }

        private void AddInvalidSong(string name)
        {
            lock (invalidLock) invalidSongsInCache.Add(name);
        }
    }
}
