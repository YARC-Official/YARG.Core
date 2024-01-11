using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public enum ScanStage
    {
        LoadingCache,
        LoadingSongs,
        Sorting,
        WritingCache,
        WritingBadSongs
    }

    public struct ScanProgressTracker
    {
        public ScanStage Stage;
        public int Count;
        public int NumScannedDirectories;
        public int BadSongCount;
    }

    public sealed partial class CacheHandler
    {
        public static ScanProgressTracker Progress => _progress;
        private static ScanProgressTracker _progress;
        public static SongCache RunScan(bool fast, string cacheLocation, string badSongsLocation, bool multithreading, bool allowDuplicates, List<string> baseDirectories)
        {
            var handler = new CacheHandler(baseDirectories, allowDuplicates);
            try
            {
                if (!fast || !handler.QuickScan(cacheLocation, multithreading))
                    handler.FullScan(!fast, cacheLocation, badSongsLocation, multithreading);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Unknown error while running song scan!");
            }

            return handler.cache;
        }

        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 23_12_12_01;

        private static readonly object dirLock = new();
        private static readonly object fileLock = new();
        private static readonly object updateLock = new();
        private static readonly object upgradeLock = new();
        private static readonly object entryLock = new();
        private static readonly object badsongsLock = new();
        private static readonly object invalidLock = new();

        static CacheHandler() { }


        private readonly SongCache cache = new();

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

        private readonly bool allowDuplicates = true;

        private CacheHandler(List<string> baseDirectories, bool allowDuplicates)
        {
            _progress = default;
            iniGroups = new(baseDirectories.Count);
            foreach (string dir in baseDirectories)
                iniGroups.TryAdd(dir, new IniGroup());
            this.allowDuplicates = allowDuplicates;
        }

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

        private bool QuickScan(string cacheLocation, bool multithreading)
        {
            try
            {
                if (!Deserialize_Quick(cacheLocation, multithreading))
                {
                    //ToastManager.ToastWarning("Song cache is not present or outdated - performing rescan");
                    return false;
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error occurred during quick cache file read!");
            }

            if (_progress.Count == 0)
            {
                //ToastManager.ToastWarning("Song cache provided zero songs - performing rescan");
                return false;
            }

            SortCategories(multithreading);
            return true;
        }

        private void FullScan(bool loadCache, string cacheLocation, string badSongsLocation, bool multithreading)
        {
            if (loadCache)
            {
                try
                {
                    Deserialize(cacheLocation, multithreading);
                }
                catch (Exception ex)
                {
                    YargTrace.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            FindNewEntries(multithreading);
            SortCategories(multithreading);

            try
            {
                Serialize(cacheLocation);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error when writing song cache!");
            }

            try
            {
                WriteBadSongs(badSongsLocation);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error when writing bad songs file!");
            }
        }

        private void SortCategories(bool multithreading)
        {
            var enums = (Instrument[])Enum.GetValues(typeof(Instrument));
            var instruments = new InstrumentCategory[enums.Length];
            for (int i = 0; i < instruments.Length; ++i)
                instruments[i] = new InstrumentCategory(enums[i]);

            void SortEntries(List<SongMetadata> entries)
            {
                foreach (var entry in entries)
                {
                    CategorySorter<string,     TitleConfig>.      Add(entry, cache.Titles);
                    CategorySorter<SortString, ArtistConfig>.     Add(entry, cache.Artists);
                    CategorySorter<SortString, AlbumConfig>.      Add(entry, cache.Albums);
                    CategorySorter<SortString, GenreConfig>.      Add(entry, cache.Genres);
                    CategorySorter<string,     YearConfig>.       Add(entry, cache.Years);
                    CategorySorter<SortString, CharterConfig>.    Add(entry, cache.Charters);
                    CategorySorter<SortString, PlaylistConfig>.   Add(entry, cache.Playlists);
                    CategorySorter<SortString, SourceConfig>.     Add(entry, cache.Sources);
                    CategorySorter<string,     ArtistAlbumConfig>.Add(entry, cache.ArtistAlbums);
                    CategorySorter<string,     SongLengthConfig>. Add(entry, cache.SongLengths);

                    foreach (var instrument in instruments)
                        instrument.Add(entry);
                }
            }

            _progress.Stage = ScanStage.Sorting;
            if (multithreading)
                Parallel.ForEach(cache.Entries, node => SortEntries(node.Value));
            else
            {
                foreach (var node in cache.Entries)
                    SortEntries(node.Value);
            }

            foreach (var instrument in instruments)
                if (instrument.Entries.Count > 0)
                    cache.Instruments.Add(instrument.Key, instrument.Entries);
        }

        private void WriteBadSongs(string badSongsLocation)
        {
            if (badSongs.Count == 0)
            {
                File.Delete(badSongsLocation);
                return;
            }

            _progress.Stage = ScanStage.WritingBadSongs;
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
                    case ScanResult.FailedSngLoad:
                        writer.WriteLine("File structure invalid or corrupted");
                        break;
                    case ScanResult.PathTooLong:
                        writer.WriteLine("Path too long for the Windows Filesystem (path limitation can be changed in registry settings if you so wish)");
                        break;
                    case ScanResult.MultipleMidiTrackNames:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous)");
                        break;
                    case ScanResult.MultipleMidiTrackNames_Update:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous) - Thrown by a midi update");
                        break;
                    case ScanResult.MultipleMidiTrackNames_Upgrade:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous) - Thrown by a pro guitar upgrade");
                        break;
                }
                writer.WriteLine();
            }
        }

        private void CreateUpdateGroup(string directory, FileInfo dta, bool removeEntries = false)
        {
            var reader = YARGDTAReader.TryCreate(dta.FullName);
            if (reader == null)
                return;

            UpdateGroup group = new(dta.LastWriteTime);
            try
            {
                while (reader!.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    group!.updates.Add(name);
                    AddUpdate(name, new(directory, new YARGDTAReader(reader)));

                    if (removeEntries)
                        RemoveCONEntry(name);
                    reader.EndNode();
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while scanning CON update folder {directory}!");
            }

            if (group!.updates.Count > 0)
                updateGroups.Add(directory, group);
            else
                YargTrace.LogWarning($"{directory} .dta file possibly malformed");
        }

        private UpgradeGroup? CreateUpgradeGroup(string directory, FileInfo dta, bool removeEntries = false)
        {
            var reader = YARGDTAReader.TryCreate(dta.FullName);
            if (reader == null)
                return null;

            UpgradeGroup group = new(dta.LastWriteTime);
            try
            {
                while (reader.StartNode())
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
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while scanning CON upgrade folder {directory}!");
            }

            if (group.upgrades.Count > 0)
            {
                upgradeGroups.Add(directory, group);
                return group;
            }

            YargTrace.LogWarning($"{directory} .dta file possibly malformed");
            return null;
        }

        private bool TryParseUpgrades(string filename, PackedCONGroup group)
        {
            var reader = group.LoadUpgrades();
            if (reader == null)
                return false;

            try
            {
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    var listing = group.CONFile.TryGetListing($"songs_upgrades/{name}_plus.mid");

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
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while scanning CON upgrades - {filename}!");
            }
            return group.Upgrades.Count > 0;
        }

        private bool AddEntry(SongMetadata entry)
        {
            var hash = entry.Hash;
            lock (entryLock)
            {
                if (cache.Entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    cache.Entries.Add(hash, new() { entry });
                ++_progress.Count;
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
