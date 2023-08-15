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
        public CacheHandler(string cacheLocation, string badSongsLocation, bool multithreading, string[] baseDirectories)
        {
            this.cacheLocation = cacheLocation;
            this.badSongsLocation = badSongsLocation;
            this.multithreading = multithreading;
            this.baseDirectories = baseDirectories;

            iniGroups = new(baseDirectories.Length);
            for (int i = 0; i < baseDirectories.Length; ++i)
                iniGroups.Add(new(baseDirectories[i]));
            cache = new(multithreading);
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
        public const int CACHE_VERSION = 23_08_12_01;

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


        private readonly SongCache cache;
        private int _count;

        private readonly List<UpdateGroup> updateGroups = new();
        private readonly List<UpgradeGroup> upgradeGroups = new();
        private readonly List<PackedCONGroup> conGroups = new();
        private readonly List<UnpackedCONGroup> extractedConGroups = new();
        private readonly List<IniGroup> iniGroups;
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
                        cache.instruments.Add(entry);
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
                        cache.instruments.Add(entry);
                    }
                }
            }
        }

        private void WriteBadSongs()
        {
            if (badSongs.Count == 0)
            {
                File.Delete(badSongsLocation);
                return;
            }

            Progress = ScanProgress.WritingBadSongs;
            using var stream = new FileStream(badSongsLocation, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);

            foreach (var error in badSongs)
            {
                writer.WriteLine(error.Key);
                switch (error.Value)
                {
                    case ScanResult.DirectoryError:
                        writer.WriteLine("Error accessing directory contents");
                        break;
                    case ScanResult.IniEntryCorruption:
                        writer.WriteLine("Corruption of either the ini file or chart/mid file");
                        break;
                    case ScanResult.NoName:
                        writer.WriteLine("Name metadata not provided");
                        break;
                    case ScanResult.NoNotes:
                        writer.WriteLine("No notes found");
                        break;
                    case ScanResult.DTAError:
                        writer.WriteLine("Error occured while parsing DTA file node");
                        break;
                    case ScanResult.MoggError:
                        writer.WriteLine("Required mogg audio file not present or used invalid encryption");
                        break;
                    case ScanResult.UnsupportedEncryption:
                        writer.WriteLine("Mogg file uses unsupported encryption");
                        break;
                    case ScanResult.MissingMidi:
                        writer.WriteLine("Midi file queried for found missing");
                        break;
                    case ScanResult.PossibleCorruption:
                        writer.WriteLine("Possible corruption of a queried midi file");
                        break;
                    case ScanResult.PathTooLong:
                        writer.WriteLine("Path too long for the Windows Filesystem (path limitation can be changed in registry settings if you so wish)");
                        break;
                }
                writer.WriteLine();
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
                var listing = file.TryGetListing($"songs_upgrades/{name}_plus.mid");

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

        private void AddCONGroup(PackedCONGroup group)
        {
            lock (conLock)
                conGroups.Add(group);
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

        private void AddExtractedCONGroup(UnpackedCONGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(group);
        }

        private void AddErrors(params object[] errors)
        {
            lock (errorLock) errorList.AddRange(errors);
        }

        private void RemoveCONEntry(string shortname)
        {
            lock (conLock)
            {
                for (int i = 0; i < conGroups.Count;)
                {
                    conGroups[i].RemoveEntries(shortname);
                    if (conGroups[i].EntryCount == 0)
                        conGroups.RemoveAt(i);
                    else
                        ++i;
                }
            }

            lock (extractedLock)
            {
                for (int i = 0; i < extractedConGroups.Count;)
                {
                    extractedConGroups[i].RemoveEntries(shortname);
                    if (extractedConGroups[i].EntryCount == 0)
                        extractedConGroups.RemoveAt(i);
                    else ++i;
                }
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
                    var upgrades = group.upgrades;
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
