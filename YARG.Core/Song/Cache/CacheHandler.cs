using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.IO;

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

            iniGroups = new(baseDirectories.Length);
            for (int i = 0; i < baseDirectories.Length; ++i)
                iniGroups.Add(baseDirectories[i], new());
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
                YargTrace.LogException(ex, "Unknown error while running song scan!");
            }
            return cache;
        }

        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 23_10_02_01;

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

        static CacheHandler() { }


        private readonly SongCache cache = new();
        private int _count;

        private readonly LockedCacheDictionary<UpdateGroup> updateGroups = new();
        private readonly LockedCacheDictionary<UpgradeGroup> upgradeGroups = new();
        private readonly LockedCacheDictionary<PackedCONGroup> conGroups = new();
        private readonly LockedCacheDictionary<UnpackedCONGroup> extractedConGroups = new();
        private readonly Dictionary<string, IniGroup> iniGroups;
        private readonly Dictionary<string, List<(string, YARGDTAReader)>> updates = new();
        private readonly Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();

        private readonly bool multithreading;
        private readonly string cacheLocation;
        private readonly string badSongsLocation;

        private IniGroup? GetBaseIniGroup(string path)
        {
            foreach (var group in iniGroups)
            {
                if (path.StartsWith(group.Key) &&
                    // Ensures directories with similar names (previously separate bases)
                    // that are consolidated in-gamne to a single base directory
                    // don't have conflicting "relative path" issues
                    (path.Length == group.Key.Length || path[group.Key.Length] == Path.DirectorySeparatorChar))
                    return group.Value;
            }
            return null;
        }

        private bool QuickScan()
        {
            try
            {
                if (!Deserialize_Quick())
                {
                    //ToastManager.ToastWarning("Song cache is not present or outdated - performing rescan");
                    return false;
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error occurred during quick cache file read!");
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
                try
                {
                    Deserialize();
                }
                catch (Exception ex)
                {
                    YargTrace.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            FindNewEntries();
            SortCategories();

            try
            {
                Serialize();
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error when writing song cache!");
            }

            try
            {
                WriteBadSongs();
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error when writing bad songs file!");
            }
        }

        private void SortCategories()
        {
            InstrumentCategory[] instruments =
            {
                new(Instrument.FiveFretGuitar),
                new(Instrument.FiveFretBass),
                new(Instrument.FiveFretRhythm),
                new(Instrument.FiveFretCoopGuitar),
                new(Instrument.SixFretGuitar),
                new(Instrument.SixFretBass),
                new(Instrument.SixFretRhythm),
                new(Instrument.SixFretCoopGuitar),
                new(Instrument.Keys),
                new(Instrument.FourLaneDrums),
                new(Instrument.ProDrums),
                new(Instrument.FiveLaneDrums),
                new(Instrument.Vocals),
                new(Instrument.Harmony),
                new(Instrument.ProGuitar_17Fret),
                new(Instrument.ProGuitar_22Fret),
                new(Instrument.ProBass_17Fret),
                new(Instrument.ProBass_22Fret),
                new(Instrument.ProKeys),
                new(Instrument.Band),
            };

            void SortEntries(List<SongMetadata> entries)
            {
                foreach (var entry in entries)
                {
                    CategorySorter<string,     TitleConfig>.      Add(entry, cache.titles);
                    CategorySorter<SortString, ArtistConfig>.     Add(entry, cache.artists);
                    CategorySorter<SortString, AlbumConfig>.      Add(entry, cache.albums);
                    CategorySorter<SortString, GenreConfig>.      Add(entry, cache.genres);
                    CategorySorter<string,     YearConfig>.       Add(entry, cache.years);
                    CategorySorter<SortString, CharterConfig>.    Add(entry, cache.charters);
                    CategorySorter<SortString, PlaylistConfig>.   Add(entry, cache.playlists);
                    CategorySorter<SortString, SourceConfig>.     Add(entry, cache.sources);
                    CategorySorter<string,     ArtistAlbumConfig>.Add(entry, cache.artistAlbums);
                    CategorySorter<string,     SongLengthConfig>. Add(entry, cache.songLengths);

                    foreach (var instrument in instruments)
                        instrument.Add(entry);
                }
            }

            Progress = ScanProgress.Sorting;
            if (multithreading)
                Parallel.ForEach(cache.entries, node => SortEntries(node.Value));
            else
            {
                foreach (var node in cache.entries)
                    SortEntries(node.Value);
            }

            foreach (var instrument in instruments)
                if (instrument.Entries.Count > 0)
                    cache.instruments.Add(instrument.Key, instrument.Entries);
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
                    case ScanResult.MissingUpdateMidi:
                        writer.WriteLine("Update Midi file queried for found missing");
                        break;
                    case ScanResult.MissingUpgradeMidi:
                        writer.WriteLine("Upgrade Midi file queried for found missing");
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
            UpdateGroup group = new(dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                group!.updates.Add(name);
                AddUpdate(name, new(directory, new YARGDTAReader(reader)));

                if (removeEntries)
                    RemoveCONEntry(name);
                reader.EndNode();
            }

            if (group!.updates.Count > 0)
                updateGroups.Add(directory, group);
        }

        private UpgradeGroup? CreateUpgradeGroup(string directory, FileInfo dta, bool removeEntries = false)
        {
            YARGDTAReader reader = new(dta.FullName);
            UpgradeGroup group = new(dta.LastWriteTime);
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
                upgradeGroups.Add(directory, group);
                return group;
            }
            return null;
        }

        private void AddCONUpgrades(PackedCONGroup group, YARGDTAReader reader)
        {
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                var listing = CONFileHandler.TryGetListing(group.Files, $"songs_upgrades/{name}_plus.mid");

                if (listing != null)
                {
                    if (CanAddUpgrade_CONInclusive(name, listing.lastWrite))
                    {
                        IRBProUpgrade upgrade = new PackedRBProUpgrade(listing, listing.lastWrite);
                        group.Upgrades[name] = upgrade;
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

        private void AddUpgrade(string name, YARGDTAReader? reader, IRBProUpgrade upgrade)
        {
            lock (upgradeLock)
                upgrades[name] = new(reader, upgrade);
        }

        private void AddUpdate(string name, (string, YARGDTAReader) node)
        {
            lock (updateLock)
            {
                if (updates.TryGetValue(name, out var list))
                    list.Add(node);
                else
                    updates[name] = new() { node };
            }
        }

        private void RemoveCONEntry(string shortname)
        {
            void Remove<T>(LockedCacheDictionary<T> dict) where T : CONGroup
            {
                lock (dict.Lock)
                {
                    foreach (var group in dict.Values)
                        if (group.Value.RemoveEntries(shortname))
                            YargTrace.DebugInfo($"{group.Key} - {shortname} pending rescan");
                }
            }
            Remove(conGroups);
            Remove(extractedConGroups);
        }

        private bool CanAddUpgrade(string shortname, DateTime lastWrite)
        {
            lock (upgradeGroups.Lock)
            {
                foreach (var group in upgradeGroups.Values)
                {
                    if (group.Value.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.LastWrite >= lastWrite)
                            return false;
                        group.Value.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }

        private bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastWrite)
        {
            lock (conGroups.Lock)
            {
                foreach (var group in conGroups.Values)
                {
                    var upgrades = group.Value.Upgrades;
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
