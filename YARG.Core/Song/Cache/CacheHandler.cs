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
        public static SongCache RunScan(bool fast, string cacheLocation, string badSongsLocation, bool multithreading, bool allowDuplicates, bool fullDirectoryPlaylists, List<string> baseDirectories)
        {
            var handler = new CacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists);
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
        public const int CACHE_VERSION = 24_02_02_01;

        private static readonly object dirLock = new();
        private static readonly object fileLock = new();
        private static readonly object updateLock = new();
        private static readonly object upgradeLock = new();
        private static readonly object entryLock = new();
        private static readonly object badsongsLock = new();
        private static readonly object invalidLock = new();

        static CacheHandler() { }


        private readonly SongCache cache = new();

        private readonly LockedList<UpdateGroup> updateGroups = new();
        private readonly LockedList<UpgradeGroup> upgradeGroups = new();
        private readonly LockedList<PackedCONGroup> conGroups = new();
        private readonly LockedList<UnpackedCONGroup> extractedConGroups = new();
        private readonly List<IniGroup> iniGroups;
        private readonly Dictionary<string, List<(string, YARGDTAReader)>> updates = new();
        private readonly Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();

        private readonly bool allowDuplicates = true;
        private readonly bool fullDirectoryPlaylists = false;
        private readonly List<SongMetadata> duplicatesRejected = new();
        private readonly List<SongMetadata> duplicatesToRemove = new();

        private CacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
        {
            _progress = default;
            this.allowDuplicates = allowDuplicates;
            this.fullDirectoryPlaylists = fullDirectoryPlaylists;

            iniGroups = new(baseDirectories.Count);
            foreach (string dir in baseDirectories)
            {
                if (!iniGroups.Exists(group => { return group.Directory == dir; }))
                {
                    iniGroups.Add(new IniGroup(dir));
                }
            }
        }

        private IniGroup? GetBaseIniGroup(string path)
        {
            foreach (var group in iniGroups)
            {
                if (path.StartsWith(group.Directory) &&
                    // Ensures directories with similar names (previously separate bases)
                    // that are consolidated in-game to a single base directory
                    // don't have conflicting "relative path" issues
                    (path.Length == group.Directory.Length || path[group.Directory.Length] == Path.DirectorySeparatorChar))
                    return group;
            }
            return null;
        }

        private bool QuickScan(string cacheLocation, bool multithreading)
        {
            try
            {
                if (!Deserialize_Quick(cacheLocation, multithreading))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error occurred during quick cache file read!");
            }

            if (_progress.Count == 0)
            {
                return false;
            }

            CleanupDuplicates();
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
            CleanupDuplicates();
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

        private void CleanupDuplicates()
        {
            static bool TryRemove<TGroup>(List<TGroup> groups, SongMetadata entry)
                where TGroup : ICacheGroup
            {
                for (int i = 0; i < groups.Count; ++i)
                {
                    var group = groups[i];
                    if (group.TryRemoveEntry(entry))
                    {
                        if (group.Count == 0)
                        {
                            groups.RemoveAt(i);
                        }
                        return true;
                    }
                }
                return false;
            }

            foreach (var entry in duplicatesToRemove)
            {
                if (TryRemove(iniGroups, entry))
                {
                    continue;
                }

                if (TryRemove(conGroups.Values, entry))
                {
                    continue;
                }

                TryRemove(extractedConGroups.Values, entry);
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
                    CategorySorter<DateTime,   DateAddedConfig>.  Add(entry, cache.DatesAdded);

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
                    case ScanResult.NoAudio:
                        writer.WriteLine("No audio accompanying the chart file");
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
                    case ScanResult.IniNotDownloaded:
                        writer.WriteLine("Ini file not fully downloaded - try again once it completes");
                        break;
                    case ScanResult.ChartNotDownloaded:
                        writer.WriteLine("Chart file not fully downloaded - try again once it completes");
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

        private void CreateUpdateGroup(string directory, AbridgedFileInfo dta, bool removeEntries)
        {
            var reader = YARGDTAReader.TryCreate(dta.FullName);
            if (reader == null)
                return;

            var group = new UpdateGroup(directory, dta.LastUpdatedTime);
            try
            {
                while (reader!.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    group.updates.Add(name);
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

            if (group.updates.Count > 0)
                updateGroups.Add(group);
            else
                YargTrace.LogWarning($"{directory} .dta file possibly malformed");
        }

        private UpgradeGroup? CreateUpgradeGroup(string directory, AbridgedFileInfo dta, bool removeEntries)
        {
            var reader = YARGDTAReader.TryCreate(dta.FullName);
            if (reader == null)
                return null;

            var group = new UpgradeGroup(directory, dta.LastUpdatedTime);
            try
            {
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    FileInfo info = new(Path.Combine(directory, $"{name}_plus.mid"));
                    if (info.Exists)
                    {
                        var abridged = new AbridgedFileInfo(info, false);
                        if (CanAddUpgrade(name, abridged.LastUpdatedTime))
                        {
                            IRBProUpgrade upgrade = new UnpackedRBProUpgrade(abridged);
                            group.upgrades[name] = upgrade;
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
                upgradeGroups.Add(group);
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
                if (!cache.Entries.TryGetValue(hash, out var list))
                {
                    cache.Entries.Add(hash, list = new List<SongMetadata>());
                }
                else if (!allowDuplicates)
                {
                    if (list[0].IsPreferedOver(entry))
                    {
                        duplicatesRejected.Add(entry);
                        return false;
                    }

                    duplicatesToRemove.Add(list[0]);
                    list[0] = entry;
                    return true;
                }
                list.Add(entry);
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
                if (!updates.TryGetValue(name, out var list))
                {
                    updates.Add(name, list = new());
                }
                list.Add(node);
            }
        }

        private void RemoveCONEntry(string shortname)
        {
            void Remove<T>(LockedList<T> dict)
                where T : CONGroup
            {
                lock (dict.Lock)
                {
                    foreach (var group in dict.Values)
                    {
                        if (group.RemoveEntries(shortname))
                        {
                            YargTrace.DebugInfo($"{group.Location} - {shortname} pending rescan");
                        }
                    }
                }
            }
            Remove(conGroups);
            Remove(extractedConGroups);
        }

        private bool CanAddUpgrade(string shortname, DateTime lastUpdated)
        {
            lock (upgradeGroups.Lock)
            {
                foreach (var group in upgradeGroups.Values)
                {
                    if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.LastUpdatedTime >= lastUpdated)
                        {
                            return false;
                        }
                        group.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }

        private bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastUpdated)
        {
            lock (conGroups.Lock)
            {
                foreach (var group in conGroups.Values)
                {
                    var upgrades = group.Upgrades;
                    if (upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade!.LastUpdatedTime >= lastUpdated)
                        {
                            return false;
                        }
                        upgrades.Remove(shortname);
                        return true;
                    }
                }
            }

            return CanAddUpgrade(shortname, lastUpdated);
        }
    }
}
