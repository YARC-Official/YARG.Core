using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

#nullable enable
namespace YARG.Core.Song.Cache
{
    public enum ScanProgress
    {
        LoadingCache,
        LoadingSongs,
        Sorting,
        WritingCache,
        WritingBadSongs
    }

    public sealed partial class CacheHandler
    {
        public CacheHandler(string cacheDirectory, string badSongsDirectory, bool multithreading, string[] baseDirectories)
        {
            cacheLocation = Path.Combine(cacheDirectory, CACHE_FILE);
            badSongsLocation = Path.Combine(badSongsDirectory, BADSONGS_FILE);
            this.multithreading = multithreading;
            this.baseDirectories = baseDirectories;
            iniGroups = new IniGroup[baseDirectories.Length];
            for (int i = 0; i < iniGroups.Length; ++i)
                iniGroups[i] = new IniGroup(baseDirectories[i]);
        }

        public SongCache RunScan(bool fast)
        {
            try
            {
                if (!fast || !QuickScan())
                    FullScan(!fast);
            }
            catch (Exception ex)
            {
                errorList.Add(ex);
            }
            return cache;
        }

        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 23_08_08_01;
        public const string CACHE_FILE = "songcache.bin";
        public const string BADSONGS_FILE = "badsongs.txt";

        public readonly List<object> errorList = new();
        public ScanProgress Progress { get; private set; }
        public int Count { get { lock (entryLock) return _count; } }
        public int NumScannedDirectories { get { lock (dirLock) return preScannedDirectories.Count; } }
        public int BadSongCount { get { lock (badsongsLock) return badSongs.Count; } }

        private static readonly object dirLock = new();
        private static readonly object fileLock = new();
        private static readonly object updateLock = new();
        private static readonly object upgradeLock = new();
        private static readonly object entryLock = new();
        private static readonly object badsongsLock = new();
        private static readonly object invalidLock = new();
        private static readonly object errorLock = new();

        private static readonly object updateGroupLock = new();
        private static readonly object upgradeGroupLock = new();
        private static readonly object extractedLock = new();
        private static readonly object conLock = new();

        static CacheHandler() { }


        private readonly SongCache cache = new();
        private int _count;

        private readonly List<UpdateGroup> updateGroups = new();
        private readonly List<UpgradeGroup> upgradeGroups = new();
        private readonly Dictionary<string, PackedCONGroup> conGroups = new();
        private readonly Dictionary<string, UnpackedCONGroup> extractedConGroups = new();
        private readonly IniGroup[] iniGroups;
        private readonly Dictionary<string, List<(string, YARGDTAReader)>> updates = new();
        private readonly Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();

        private readonly bool multithreading;
        private readonly string[] baseDirectories = Array.Empty<string>();
        private readonly string cacheLocation;
        private readonly string badSongsLocation;

        private int GetBaseDirectoryIndex(string path)
        {
            for (int i = 0; i != baseDirectories.Length; ++i)
                if (path.StartsWith(baseDirectories[i]))
                    return i;
            return -1;
        }

        private bool QuickScan()
        {
            if (!Deserialize_Quick())
            {
                //ToastManager.ToastWarning("Song cache is not present or outdated - performing rescan");
                return false;
            }

            if (Count == 0)
            {
                //ToastManager.ToastWarning("Song cache provided zero songs - performing rescan");
                return false;
            }

            SortCategories();
            return true;
        }

        private void FullScan(bool loadCache)
        {
            if (loadCache)
            {
                Deserialize();
            }

            FindNewEntries();
            SortCategories();

            try
            {
                Serialize();
            }
            catch (Exception ex)
            {
                YargTrace.LogError(ex.Message);
            }

            try
            {
                WriteBadSongs();
            }
            catch (Exception ex)
            {
                YargTrace.LogError(ex.Message);
            }
        }

        private void SortCategories()
        {
            Progress = ScanProgress.Sorting;
            if (multithreading)
            {
                Parallel.ForEach(cache.entries, entryList =>
                {
                    foreach (var entry in entryList.Value)
                    {
                        cache.titles.Add(entry);
                        cache.artists.Add(entry);
                        cache.albums.Add(entry);
                        cache.genres.Add(entry);
                        cache.years.Add(entry);
                        cache.charters.Add(entry);
                        cache.playlists.Add(entry);
                        cache.sources.Add(entry);
                        cache.artistAlbums.Add(entry);
                        cache.songLengths.Add(entry);
                    }
                });
            }
            else
            {
                foreach (var entryList in cache.entries)
                {
                    foreach (var entry in entryList.Value)
                    {
                        cache.titles.Add(entry);
                        cache.artists.Add(entry);
                        cache.albums.Add(entry);
                        cache.genres.Add(entry);
                        cache.years.Add(entry);
                        cache.charters.Add(entry);
                        cache.playlists.Add(entry);
                        cache.sources.Add(entry);
                        cache.artistAlbums.Add(entry);
                        cache.songLengths.Add(entry);
                    }
                }
            }
        }

        private void WriteBadSongs()
        {
            Progress = ScanProgress.WritingBadSongs;
            using var stream = new FileStream(badSongsLocation, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);

            foreach (var error in badSongs)
            {
                writer.WriteLineAsync(error.Key);
                switch (error.Value)
                {
                    case ScanResult.DirectoryError:
                        writer.WriteLineAsync("Error accessing directory contents");
                        break;
                    case ScanResult.IniEntryCorruption:
                        writer.WriteLineAsync("Corruption of either the ini file or chart/mid file");
                        break;
                    case ScanResult.NoName:
                        writer.WriteLineAsync("Name metadata not provided");
                        break;
                    case ScanResult.NoNotes:
                        writer.WriteLineAsync("No notes found");
                        break;
                    case ScanResult.DTAError:
                        writer.WriteLineAsync("Error occured while parsing DTA file node");
                        break;
                    case ScanResult.MoggError:
                        writer.WriteLineAsync("Required mogg audio file not present or used invalid encryption");
                        break;
                    case ScanResult.UnsupportedEncryption:
                        writer.WriteLineAsync("Mogg file uses unsupported encryption");
                        break;
                    case ScanResult.MissingMidi:
                        writer.WriteLineAsync("Midi file queried for found missing");
                        break;
                    case ScanResult.PossibleCorruption:
                        writer.WriteLineAsync("Possible corruption of a queried midi file");
                        break;
                }
                writer.WriteLineAsync();
            }
        }

        private void CreateUpdateGroup(string directory, FileInfo dta, bool removeEntries = false)
        {
            YARGDTAReader reader = new(dta.FullName);
            UpdateGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                group!.updates.Add(name);

                (string, YARGDTAReader) node = new(directory, new YARGDTAReader(reader));
                lock (updateLock)
                {
                    if (updates.TryGetValue(name, out var list))
                        list.Add(node);
                    else
                        updates[name] = new() { node };
                }

                if (removeEntries)
                    RemoveCONEntry(name);
                reader.EndNode();
            }

            if (group!.updates.Count > 0)
                AddUpdateGroup(group);
        }

        private UpgradeGroup? CreateUpgradeGroup(string directory, FileInfo dta, bool removeEntries = false)
        {
            YARGDTAReader reader = new(dta.FullName);
            UpgradeGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                FileInfo file = new(Path.Combine(directory, $"{name}_plus.mid"));
                if (file.Exists)
                {
                    if (CanAddUpgrade(name, file.LastWriteTime))
                    {
                        IRBProUpgrade upgrade = new UnpackedRBProUpgrade(file.FullName, file.LastWriteTime);
                        group!.upgrades[name] = upgrade;
                        AddUpgrade(name, new YARGDTAReader(reader), upgrade);

                        if (removeEntries)
                            RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }

            if (group.upgrades.Count > 0)
            {
                AddUpgradeGroup(group);
                return group;
            }
            return null;
        }

        private void AddCONUpgrades(PackedCONGroup group, YARGDTAReader reader)
        {
            var file = group.file;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                var listing = file[$"songs_upgrades/{name}_plus.mid"];

                if (listing != null)
                {
                    if (CanAddUpgrade_CONInclusive(name, listing.lastWrite))
                    {
                        IRBProUpgrade upgrade = new PackedRBProUpgrade(file, listing, listing.lastWrite);
                        group.upgrades[name] = upgrade;
                        AddUpgrade(name, new YARGDTAReader(reader), upgrade);
                        RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }
        }

        private bool AddEntry(SongMetadata entry)
        {
            var hash = entry.Hash;
            lock (entryLock)
            {
                if (cache.entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    cache.entries.Add(hash, new() { entry });
                ++_count;
            }
            return true;
        }

        private void AddIniEntry(SongMetadata entry, int index)
        {
            iniGroups[index].AddEntry(entry);
        }

        private void AddUpgrade(string name, YARGDTAReader? reader, IRBProUpgrade upgrade)
        {
            lock (upgradeLock)
                upgrades[name] = new(reader, upgrade);
        }

        private void AddCONGroup(string filename, PackedCONGroup group)
        {
            lock (conLock)
                conGroups.Add(filename, group);
        }

        private void AddUpdateGroup(UpdateGroup group)
        {
            lock (updateGroupLock)
                updateGroups.Add(group);
        }

        private void AddUpgradeGroup(UpgradeGroup group)
        {
            lock (upgradeGroupLock)
                upgradeGroups.Add(group);
        }

        private void AddExtractedCONGroup(string directory, UnpackedCONGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(directory, group);
        }

        private void AddErrors(params object[] errors)
        {
            lock (errorLock) errorList.AddRange(errors);
        }

        private void RemoveCONEntry(string shortname)
        {
            lock (conLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in conGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    conGroups.Remove(entriesToRemove[i]);
            }

            lock (extractedLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in extractedConGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    extractedConGroups.Remove(entriesToRemove[i]);
            }
        }

        private bool CanAddUpgrade(string shortname, DateTime lastWrite)
        {
            lock (upgradeGroupLock)
            {
                foreach (var group in upgradeGroups)
                {
                    if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.LastWrite >= lastWrite)
                            return false;
                        group.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }


        private bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastWrite)
        {
            lock (conLock)
            {
                foreach (var group in conGroups)
                {
                    var upgrades = group.Value.upgrades;
                    if (upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade!.LastWrite >= lastWrite)
                            return false;
                        upgrades.Remove(shortname);
                        return true;
                    }
                }
            }

            return CanAddUpgrade(shortname, lastWrite);
        }
    }
}
