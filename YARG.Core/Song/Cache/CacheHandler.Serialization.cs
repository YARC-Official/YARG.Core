using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public abstract partial class CacheHandler
    {
        public const int SIZEOF_DATETIME = 8;
        protected readonly HashSet<string> invalidSongsInCache = new();

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        ///
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        private static FileStream? CheckCacheFile(string cacheLocation, bool fullDirectoryPlaylists)
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargLogger.LogDebug("Cache invalid or not found");
                return null;
            }

            var fs = new FileStream(cacheLocation, FileMode.Open, FileAccess.Read);
            using var counter = DisposableCounter.Wrap(fs);
            if (fs.Read<int>(Endianness.Little) != CACHE_VERSION)
            {
                YargLogger.LogDebug($"Cache outdated");
                return null;
            }

            if (fs.ReadBoolean() != fullDirectoryPlaylists)
            {
                YargLogger.LogDebug($"FullDirectoryFlag flipped");
                return null;
            }

            return counter.Release();
        }

        protected abstract void Deserialize(FileStream stream);
        protected abstract void Deserialize_Quick(FileStream stream);
        protected abstract PackedCONGroup? FindCONGroup(string filename);

        protected virtual void AddInvalidSong(string name)
        {
            invalidSongsInCache.Add(name);
        }

        private void Serialize(string cacheLocation)
        {
            using var writer = new BinaryWriter(new FileStream(cacheLocation, FileMode.Create, FileAccess.Write));
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            writer.Write(CACHE_VERSION);
            writer.Write(fullDirectoryPlaylists);

            CategoryWriter.WriteToCache(writer, cache.Titles, SongAttribute.Name, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Artists, SongAttribute.Artist, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Albums, SongAttribute.Album, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Genres, SongAttribute.Genre, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Years, SongAttribute.Year, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Charters, SongAttribute.Charter, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Playlists, SongAttribute.Playlist, ref nodes);
            CategoryWriter.WriteToCache(writer, cache.Sources, SongAttribute.Source, ref nodes);

            List<PackedCONGroup> upgradeCons = new();
            List<PackedCONGroup> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Upgrades.Count > 0)
                    upgradeCons.Add(group);

                if (group.Count > 0)
                    entryCons.Add(group);
            }

            ICacheGroup<IniSubEntry>.SerializeGroups(iniGroups, writer, nodes);
            IModificationGroup.SerializeGroups(updateGroups, writer);
            IModificationGroup.SerializeGroups(upgradeGroups, writer);
            IModificationGroup.SerializeGroups(upgradeCons, writer);
            ICacheGroup<RBCONEntry>.SerializeGroups(entryCons, writer, nodes);
            ICacheGroup<RBCONEntry>.SerializeGroups(extractedConGroups, writer, nodes);
        }

        protected void ReadIniEntry(string baseDirectory, IniGroup group, BinaryReader reader, CategoryCacheStrings strings)
        {
            bool isSngEntry = reader.ReadBoolean();
            var entry = isSngEntry ?
                SngEntry.TryLoadFromCache(baseDirectory, reader, strings) :
                UnpackedIniEntry.TryLoadFromCache(baseDirectory, reader, strings);

            if (entry == null)
            {
                YargLogger.LogDebug($"Ini entry invalid {baseDirectory}");
                return;
            }

            string root = entry.Directory;
            if (!isSngEntry)
            {
                if (Directory.EnumerateDirectories(root).Any())
                {
                    AddToBadSongs(root, ScanResult.LooseChart_Warning);
                }
                FindOrMarkDirectory(root);
            }
            else
            {
                FindOrMarkFile(root);
            }

            AddEntry(entry);
            group.AddEntry(entry);
        }

        protected void ReadUpdateDirectory(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var dtaLastWritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                var dtaInfo = new FileInfo(Path.Combine(directory, "songs_updates.dta"));
                if (dtaInfo.Exists)
                {
                    FindOrMarkDirectory(directory);

                    var dirInfo = new DirectoryInfo(directory);
                    var group = CreateUpdateGroup(dirInfo, dtaInfo, false);
                    if (group != null && dtaInfo.LastWriteTime == dtaLastWritten)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            if (group.Updates.TryGetValue(name, out var update))
                            {
                                if (!update.Validate(reader))
                                {
                                    AddInvalidSong(name);
                                }
                            }
                            else
                            {
                                AddInvalidSong(name);
                                SongUpdate.SkipRead(reader);
                            }
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                SongUpdate.SkipRead(reader);
            }
        }

        protected void ReadUpgradeDirectory(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var dtaLastWrritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) != null)
            {
                var dtaInfo = new FileInfo(Path.Combine(directory, "upgrades.dta"));
                if (dtaInfo.Exists)
                {
                    FindOrMarkDirectory(directory);

                    var group = CreateUpgradeGroup(directory, dtaInfo, false);
                    if (group != null && dtaInfo.LastWriteTime == dtaLastWrritten)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            var lastUpdated = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.Upgrades.TryGetValue(name, out var upgrade) || upgrade!.LastUpdatedTime != lastUpdated)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                reader.Move(SIZEOF_DATETIME);
            }
        }

        protected void ReadUpgradeCON(BinaryReader reader)
        {
            string filename = reader.ReadString();
            var conLastUpdated = DateTime.FromBinary(reader.ReadInt64());
            var dtaLastWritten = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup != null)
            {
                // Make playlist as the group is only made once
                string playlist = ConstructPlaylist(filename, baseGroup.Directory);
                var group = CreateCONGroup(filename, playlist);
                if (group == null)
                {
                    goto Invalidate;
                }

                AddPackedCONGroup(group);

                if (TryParseUpgrades(filename, group) && group.UpgradeDta!.LastWrite == dtaLastWritten)
                {
                    if (group.Info.LastUpdatedTime != conLastUpdated)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (group.Upgrades[name].LastUpdatedTime != lastWrite)
                            {
                                AddInvalidSong(name);
                            }
                        }
                    }
                    return;
                }
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadString());
                reader.Move(SIZEOF_DATETIME);
            }
        }

        protected PackedCONGroup? ReadCONGroupHeader(BinaryReader reader)
        {
            string filename = reader.ReadString();
            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            var group = FindCONGroup(filename);
            if (group == null)
            {
                var info = new FileInfo(filename);
                if (!info.Exists)
                {
                    return null;
                }

                FindOrMarkFile(filename);

                var abridged = new AbridgedFileInfo(info);
                var confile = CONFile.TryParseListings(abridged);
                if (confile == null)
                {
                    return null;
                }

                string playlist = ConstructPlaylist(filename, baseGroup.Directory);
                group = new PackedCONGroup(confile.Value, abridged, playlist);
                AddPackedCONGroup(group);
            }

            if (group.SongDTA == null || group.SongDTA.LastWrite != dtaLastWrite)
            {
                return null;
            }
            return group;
        }

        protected UnpackedCONGroup? ReadExtractedCONGroupHeader(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var baseGroup = GetBaseIniGroup(directory);
            if (baseGroup == null)
            {
                return null;
            }

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
            {
                return null;
            }

            FindOrMarkDirectory(directory);

            string playlist = ConstructPlaylist(directory, baseGroup.Directory);
            var group = new UnpackedCONGroup(directory, dtaInfo, playlist);
            AddUnpackedCONGroup(group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
            {
                return null;
            }
            return group;
        }

        protected void ReadCONGroup(BinaryReader reader, Action<string, int, BinaryReader> func)
        {
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
                func(name, index, entryReader);
            }
        }

        protected void QuickReadIniEntry(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            var entry = reader.ReadBoolean() ?
                SngEntry.LoadFromCache_Quick(baseDirectory, reader, strings) :
                UnpackedIniEntry.IniFromCache_Quick(baseDirectory, reader, strings);

            if (entry != null)
            {
                AddEntry(entry);
            }
            else
            {
                YargLogger.LogError("Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
            }
        }

        protected void QuickReadUpgradeDirectory(BinaryReader reader)
        {
            string directory = reader.ReadString();
            var dtaLastUpdated = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            var group = new UpgradeGroup(directory, dtaLastUpdated);
            AddUpgradeGroup(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                var info = new AbridgedFileInfo_Length(filename, reader);
                var upgrade = new UnpackedRBProUpgrade(info);
                group.Upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        protected void QuickReadUpgradeCON(BinaryReader reader)
        {
            string filename = reader.ReadString();
            reader.Move(2 * SIZEOF_DATETIME);
            int count = reader.ReadInt32();

            var group = CreateCONGroup(filename, string.Empty);
            if (group != null)
            {
                AddPackedCONGroup(group);
            }

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                var listing = default(CONFileListing);
                group?.ConFile.TryGetListing($"songs_upgrades/{name}_plus.mid", out listing);

                var upgrade = new PackedRBProUpgrade(listing, lastWrite);
                AddUpgrade(name, null, upgrade);
            }
        }

        protected PackedCONGroup? QuickReadCONGroupHeader(BinaryReader reader)
        {
            string filename = reader.ReadString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());

            var group = FindCONGroup(filename);
            if (group == null)
            {
                group = CreateCONGroup(filename, string.Empty);
                if (group == null)
                {
                    return null;
                }

                AddPackedCONGroup(group);
            }

            if (group.SongDTA == null || group.SongDTA.LastWrite != dtaLastWrite)
            {
                return null;
            }
            return group;
        }

        private PackedCONGroup? CreateCONGroup(string filename, string defaultPlaylist)
        {
            var info = new FileInfo(filename);
            if (!info.Exists)
                return null;

            FindOrMarkFile(filename);

            var abridged = new AbridgedFileInfo(info);
            var confile = CONFile.TryParseListings(abridged);
            if (confile == null)
            {
                return null;
            }
            return new PackedCONGroup(confile.Value, abridged, defaultPlaylist);
        }

        private string ConstructPlaylist(string filename, string baseDirectory)
        {
            string directory = Path.GetDirectoryName(filename);
            if (directory.Length == baseDirectory.Length)
            {
                return "Unknown Playlist";
            }

            if (!fullDirectoryPlaylists)
            {
                return Path.GetFileName(directory);
            }
            return directory[(baseDirectory.Length + 1)..];
        }
    }
}
