using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public abstract partial class CacheHandler
    {
        public const int SIZEOF_DATETIME = 8;
        protected readonly HashSet<string> invalidSongsInCache = new();
        private readonly Dictionary<string, FileCollection> collectionCache = new();

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        ///
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        private static AllocatedArray<byte>? LoadCacheToMemory(string cacheLocation, bool fullDirectoryPlaylists)
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargLogger.LogDebug("Cache invalid or not found");
                return null;
            }

            using var stream = new FileStream(cacheLocation, FileMode.Open, FileAccess.Read);
            if (stream.Read<int>(Endianness.Little) != CACHE_VERSION)
            {
                YargLogger.LogDebug($"Cache outdated");
                return null;
            }

            if (stream.ReadBoolean() != fullDirectoryPlaylists)
            {
                YargLogger.LogDebug($"FullDirectoryFlag flipped");
                return null;
            }
            return AllocatedArray<byte>.Read(stream, info.Length - stream.Position);
        }

        protected abstract void Deserialize(UnmanagedMemoryStream stream);
        protected abstract void Deserialize_Quick(UnmanagedMemoryStream stream);
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

        protected void ReadIniEntry(string baseDirectory, IniGroup group, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            bool isSngEntry = stream.ReadBoolean();
            var entry = isSngEntry ?
                SngEntry.TryLoadFromCache(baseDirectory, stream, strings) :
                UnpackedIniEntry.TryLoadFromCache(baseDirectory, stream, strings);

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

        protected void ReadUpdateDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) == null)
            {
                goto Invalidate;
            }

            var dirInfo = new DirectoryInfo(directory);
            if (!dirInfo.Exists)
            {
                goto Invalidate;
            }

            var collection = new FileCollection(dirInfo);
            if (!collection.subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
            {
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            var group = CreateUpdateGroup(collection, dta, false);
            if (group == null)
            {
                goto Invalidate;
            }

            AddUpdateGroup(group);
            FindOrMarkDirectory(directory);
            
            if (group.DTALastWrite != dtaLastWritten)
            {
                goto Invalidate;
            }

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                if (group.Updates.TryGetValue(name, out var update))
                {
                    if (!update.Validate(stream))
                    {
                        AddInvalidSong(name);
                    }
                }
                else
                {
                    AddInvalidSong(name);
                    SongUpdate.SkipRead(stream);
                }
            }
            return;

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                SongUpdate.SkipRead(stream);
            }
        }

        protected void ReadUpgradeDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) == null)
            {
                goto Invalidate;
            }

            var dirInfo = new DirectoryInfo(directory);
            if (!dirInfo.Exists)
            {
                goto Invalidate;
            }

            var collection = new FileCollection(dirInfo);
            if (!collection.subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
            {
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            var group = CreateUpgradeGroup(in collection, dta, false);
            if (group == null)
            {
                goto Invalidate;
            }

            AddUpgradeGroup(group);
            if (dta.LastWriteTime != dtaLastWritten)
            {
                goto Invalidate;
            }

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                if (!group.Upgrades.TryGetValue(name, out var upgrade) || upgrade!.LastUpdatedTime != lastUpdated)
                    AddInvalidSong(name);
            }
            return;

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                stream.Position += SIZEOF_DATETIME;
            }
        }

        protected void ReadUpgradeCON(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var conLastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

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
                            string name = stream.ReadString();
                            var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
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
                AddInvalidSong(stream.ReadString());
                stream.Position += SIZEOF_DATETIME;
            }
        }

        protected PackedCONGroup? ReadCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
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

        protected UnpackedCONGroup? ReadExtractedCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var baseGroup = GetBaseIniGroup(directory);
            if (baseGroup == null)
            {
                return null;
            }

            var dtaInfo = new FileInfo(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
            {
                return null;
            }

            FindOrMarkDirectory(directory);

            string playlist = ConstructPlaylist(directory, baseGroup.Directory);
            var group = new UnpackedCONGroup(directory, dtaInfo, playlist);
            AddUnpackedCONGroup(group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(stream.Read<long>(Endianness.Little)))
            {
                return null;
            }
            return group;
        }

        protected void ReadCONGroup(UnmanagedMemoryStream stream, Action<string, int, UnmanagedMemoryStream> func)
        {
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; ++i)
            {
                string name = stream.ReadString();
                int index = stream.Read<int>(Endianness.Little);
                int length = stream.Read<int>(Endianness.Little);
                if (invalidSongsInCache.Contains(name))
                {
                    stream.Position += length;
                    continue;
                }

                var entryReader = stream.Slice(length);
                func(name, index, entryReader);
            }
        }

        protected void QuickReadIniEntry(string baseDirectory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var entry = stream.ReadBoolean() ?
                SngEntry.LoadFromCache_Quick(baseDirectory, stream, strings) :
                UnpackedIniEntry.IniFromCache_Quick(baseDirectory, stream, strings);

            if (entry != null)
            {
                AddEntry(entry);
            }
            else
            {
                YargLogger.LogError("Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
            }
        }

        protected void QuickReadUpgradeDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                var info = new AbridgedFileInfo_Length(filename, stream);
                AddUpgrade(name, default, new UnpackedRBProUpgrade(info));
            }
        }

        protected void QuickReadUpgradeCON(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            stream.Position += 2 * SIZEOF_DATETIME;
            int count = stream.Read<int>(Endianness.Little);

            var group = CreateCONGroup(filename, string.Empty);
            if (group != null)
            {
                AddPackedCONGroup(group);
            }

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                var listing = default(CONFileListing);
                group?.ConFile.TryGetListing($"songs_upgrades/{name}_plus.mid", out listing);

                var upgrade = new PackedRBProUpgrade(listing, lastWrite);
                AddUpgrade(name, default, upgrade);
            }
        }

        protected PackedCONGroup? QuickReadCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var dtaLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));

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

    internal static class UnmanagedStreamSlicer
    {
        public static unsafe UnmanagedMemoryStream Slice(this UnmanagedMemoryStream stream, int length)
        {
            var newStream = new UnmanagedMemoryStream(stream.PositionPointer, length);
            stream.Position += length;
            return newStream;
        }
    }
}
