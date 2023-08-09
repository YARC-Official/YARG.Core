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

        private FileStream? CheckCacheFile()
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < 28)
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
                List<Task> entryTasks = new();
                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => ReadIniGroup_Parallel(reader, strings)));
                }

                List<Task> conTasks = new();
                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    conTasks.Add(Task.Run(() => ReadUpdateDirectory(reader)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    conTasks.Add(Task.Run(() => ReadUpgradeDirectory(reader)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    conTasks.Add(Task.Run(() => ReadUpgradeCON(reader)));
                }

                Task.WaitAll(conTasks.ToArray());

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => ReadCONGroup_Parallel(reader, strings)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => ReadExtractedCONGroup_Parallel(reader, strings)));
                }

                Task.WaitAll(entryTasks.ToArray());
            }
            else
            {
                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadIniGroup(new(stream.ReadBytes(length)), strings);
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadUpdateDirectory(new(stream.ReadBytes(length)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadUpgradeDirectory(new(stream.ReadBytes(length)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadUpgradeCON(new(stream.ReadBytes(length)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadCONGroup(new(stream.ReadBytes(length)), strings);
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    ReadExtractedCONGroup(new(stream.ReadBytes(length)), strings);
                }
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
                List<Task> entryTasks = new();
                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => QuickReadIniGroup_Parallel(reader, strings)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    stream.Position += length;
                }

                List<Task> conTasks = new();
                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    conTasks.Add(Task.Run(() => QuickReadUpgradeDirectory(reader)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    conTasks.Add(Task.Run(() => QuickReadUpgradeCON(reader)));
                }

                Task.WaitAll(conTasks.ToArray());

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => QuickReadCONGroup_Parallel(reader, strings)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    YARGBinaryReader reader = new(stream.ReadBytes(length));
                    entryTasks.Add(Task.Run(() => QuickReadExtractedCONGroup_Parallel(reader, strings)));
                }

                Task.WaitAll(entryTasks.ToArray());
            }
            else
            {
                int count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    QuickReadIniGroup(new(stream.ReadBytes(length)), strings);
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    stream.Position += length;
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    QuickReadUpgradeDirectory(new(stream.ReadBytes(length)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    QuickReadUpgradeCON(new(stream.ReadBytes(length)));
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    QuickReadCONGroup(new(stream.ReadBytes(length)), strings);
                }

                count = stream.ReadInt32LE();
                for (int i = 0; i < count; ++i)
                {
                    int length = stream.ReadInt32LE();
                    QuickReadExtractedCONGroup(new(stream.ReadBytes(length)), strings);
                }
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

            writer.Write(iniGroups.Length);
            foreach (var group in iniGroups)
            {
                byte[] buffer = group.Serialize(nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(updateGroups.Count);
            foreach (var group in updateGroups)
            {
                byte[] buffer = group.Serialize();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(upgradeGroups.Count);
            foreach (var group in upgradeGroups)
            {
                byte[] buffer = group.Serialize();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            List<KeyValuePair<string, PackedCONGroup>> upgradeCons = new();
            List<KeyValuePair<string, PackedCONGroup>> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Value.UpgradeCount > 0)
                    upgradeCons.Add(group);

                if (group.Value.EntryCount > 0)
                    entryCons.Add(group);
            }

            writer.Write(upgradeCons.Count);
            foreach (var group in upgradeCons)
            {
                byte[] buffer = group.Value.FormatUpgradesForCache(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(entryCons.Count);
            foreach (var group in entryCons)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key, ref nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(extractedConGroups.Count);
            foreach (var group in extractedConGroups)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key, ref nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
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
                AddCONGroup(filename, group!);
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
                AddCONGroup(filename, group);
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

            UnpackedCONGroup group = new(dtaInfo);
            MarkDirectory(directory);
            AddExtractedCONGroup(directory, group);

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
                AddCONGroup(filename, group);

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
                AddCONGroup(filename, group!);
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
                return conGroups.TryGetValue(filename, out group);
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
