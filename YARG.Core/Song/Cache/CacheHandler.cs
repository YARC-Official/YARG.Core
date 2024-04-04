using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

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

    public abstract partial class CacheHandler
    {
        public static ScanProgressTracker Progress => _progress;
        private static ScanProgressTracker _progress;

        public static SongCache RunScan(bool fast, string cacheLocation, string badSongsLocation, bool multithreading, bool allowDuplicates, bool fullDirectoryPlaylists, List<string> baseDirectories)
        {
            CacheHandler handler = multithreading
                ? new ParallelCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists)
                : new SequentialCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists);

            try
            {
                if (!fast || !QuickScan(handler, cacheLocation))
                    FullScan(handler, !fast, cacheLocation, badSongsLocation);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Unknown error while running song scan!");
            }

            return handler.cache;
        }

        private static bool QuickScan(CacheHandler handler, string cacheLocation)
        {
            try
            {
                using var stream = CheckCacheFile(cacheLocation, handler.fullDirectoryPlaylists);
                if (stream == null)
                {
                    return false;
                }

                _progress.Stage = ScanStage.LoadingCache;
                YargLogger.LogDebug("Quick Read start");
                handler.Deserialize_Quick(stream);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error occurred during quick cache file read!");
            }

            if (_progress.Count == 0)
            {
                return false;
            }

            handler.CleanupDuplicates();

            _progress.Stage = ScanStage.Sorting;
            handler.SortCategories();
            YargLogger.LogDebug($"Total Entries: {_progress.Count}");
            return true;
        }

        private static void FullScan(CacheHandler handler, bool loadCache, string cacheLocation, string badSongsLocation)
        {
            if (loadCache)
            {
                try
                {
                    using var stream = CheckCacheFile(cacheLocation, handler.fullDirectoryPlaylists);
                    if (stream != null)
                    {
                        _progress.Stage = ScanStage.LoadingCache;
                        YargLogger.LogDebug("Full Read start");
                        handler.Deserialize(stream);
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            _progress.Stage = ScanStage.LoadingSongs;
            handler.FindNewEntries();
            handler.CleanupDuplicates();

            _progress.Stage = ScanStage.Sorting;
            handler.SortCategories();
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);

            try
            {
                _progress.Stage = ScanStage.WritingCache;
                handler.Serialize(cacheLocation);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error when writing song cache!");
            }

            try
            {
                if (handler.badSongs.Count > 0)
                {
                    _progress.Stage = ScanStage.WritingBadSongs;
                    handler.WriteBadSongs(badSongsLocation);
                }
                else
                {
                    File.Delete(badSongsLocation);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error when writing bad songs file!");
            }
        }

        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 24_04_04_03;

        protected readonly SongCache cache = new();

        protected readonly List<IniGroup> iniGroups;
        protected readonly List<UpdateGroup> updateGroups = new();
        protected readonly List<UpgradeGroup> upgradeGroups = new();
        protected readonly List<PackedCONGroup> conGroups = new();
        protected readonly List<UnpackedCONGroup> extractedConGroups = new();

        protected readonly Dictionary<string, List<SongUpdate>> updates = new();
        protected readonly Dictionary<string, (YARGDTAReader?, IRBProUpgrade)> upgrades = new();
        protected readonly HashSet<string> preScannedDirectories = new();
        protected readonly HashSet<string> preScannedFiles = new();


        protected readonly bool allowDuplicates = true;
        protected readonly bool fullDirectoryPlaylists;
        protected readonly List<SongEntry> duplicatesRejected = new();
        protected readonly List<SongEntry> duplicatesToRemove = new();
        protected readonly SortedDictionary<string, ScanResult> badSongs = new();

        protected CacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
        {
            _progress = default;
            this.allowDuplicates = allowDuplicates;
            this.fullDirectoryPlaylists = fullDirectoryPlaylists;

            iniGroups = new(baseDirectories.Count);
            foreach (string dir in baseDirectories)
            {
                if (!string.IsNullOrEmpty(dir) && !iniGroups.Exists(group => { return group.Directory == dir; }))
                {
                    iniGroups.Add(new IniGroup(dir));
                }
            }
        }

        protected abstract void SortEntries(InstrumentCategory[] instruments);
        protected abstract void AddUpdates(UpdateGroup group, Dictionary<string, List<YARGDTAReader>> nodes, bool removeEntries);
        protected abstract void AddUpgrade(string name, YARGDTAReader? reader, IRBProUpgrade upgrade);
        protected abstract void AddPackedCONGroup(PackedCONGroup group);
        protected abstract void AddUnpackedCONGroup(UnpackedCONGroup group);
        protected abstract void AddUpgradeGroup(UpgradeGroup group);
        protected abstract void RemoveCONEntry(string shortname);
        protected abstract bool CanAddUpgrade(string shortname, DateTime lastUpdated);
        protected abstract bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastUpdated);
        protected virtual bool AddEntry(SongEntry entry)
        {
            var hash = entry.Hash;
            if (cache.Entries.TryGetValue(hash, out var list) && !allowDuplicates)
            {
                if (list[0].IsPreferedOver(entry))
                {
                    duplicatesRejected.Add(entry);
                    return false;
                }

                duplicatesToRemove.Add(list[0]);
                list[0] = entry;
            }
            else
            {
                if (list == null)
                {
                    cache.Entries.Add(hash, list = new List<SongEntry>());
                }

                list.Add(entry);
                ++_progress.Count;
            }
            return true;
        }

        protected IniGroup? GetBaseIniGroup(string path)
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

        private void CleanupDuplicates()
        {
            static bool TryRemove<TGroup, TEntry>(List<TGroup> groups, SongEntry entry)
                where TGroup : ICacheGroup<TEntry>
                where TEntry : SongEntry
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
                if (TryRemove<IniGroup, IniSubEntry>(iniGroups, entry))
                {
                    continue;
                }

                if (TryRemove<PackedCONGroup, RBCONEntry>(conGroups, entry))
                {
                    continue;
                }

                TryRemove<UnpackedCONGroup, RBCONEntry>(extractedConGroups, entry);
            }
        }

        private void SortCategories()
        {
            var enums = (Instrument[])Enum.GetValues(typeof(Instrument));
            var instruments = new InstrumentCategory[enums.Length];
            for (int i = 0; i < instruments.Length; ++i)
                instruments[i] = new InstrumentCategory(enums[i]);

            SortEntries(instruments);

            foreach (var instrument in instruments)
                if (instrument.Entries.Count > 0)
                    cache.Instruments.Add(instrument.Key, instrument.Entries);
        }

        private void WriteBadSongs(string badSongsLocation)
        {
            using var stream = new FileStream(badSongsLocation, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);

            writer.WriteLine($"Total Errors: {badSongs.Count}");
            writer.WriteLine();

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
                    case ScanResult.LooseChart_Warning:
                        writer.WriteLine("Further subdirectory traversal halted by a possibly loose chart. To fix, if desired, place the loose chart files in their own dedicated folder.");
                        break;
                }
                writer.WriteLine();
            }
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
                            var upgrade = new UnpackedRBProUpgrade(abridged);
                            group.Upgrades[name] = upgrade;
                            AddUpgrade(name, reader.Clone(), upgrade);

                            if (removeEntries)
                            {
                                RemoveCONEntry(name);
                            }
                        }
                    }

                    reader.EndNode();
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while scanning CON upgrade folder {directory}!");
            }

            if (group.Upgrades.Count == 0)
            {
                YargLogger.LogFormatWarning("{0} .dta file possibly malformed", directory);
                return null;
            }
            AddUpgradeGroup(group);
            return group;
        }

        private UpdateGroup? CreateUpdateGroup(DirectoryInfo dirInfo, AbridgedFileInfo dta, bool removeEntries)
        {
            var nodes = FindUpdateNodes(dirInfo.FullName, dta);
            if (nodes == null)
            {
                return null;
            }

            var group = new UpdateGroup(dirInfo, dta.LastUpdatedTime);
            AddUpdates(group, nodes, removeEntries);
            return group;
        }

        private Dictionary<string, List<YARGDTAReader>>? FindUpdateNodes(string directory, AbridgedFileInfo dta)
        {
            var reader = YARGDTAReader.TryCreate(dta.FullName);
            if (reader == null)
                return null;

            var nodes = new Dictionary<string, List<YARGDTAReader>>();
            try
            {
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    if (!nodes.TryGetValue(name, out var list))
                    {
                        nodes.Add(name, list = new List<YARGDTAReader>());
                    }
                    list.Add(reader.Clone());
                    reader.EndNode();
                }

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while scanning CON update folder {directory}!");
                return null;
            }

            if (nodes.Count == 0)
            {
                YargLogger.LogFormatWarning("{0} .dta file possibly malformed", directory);
                return null;
            }
            return nodes;
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
                            AddUpgrade(name, reader.Clone(), upgrade);
                            RemoveCONEntry(name);
                        }
                    }

                    reader.EndNode();
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while scanning CON upgrades - {filename}!");
            }
            return group.Upgrades.Count > 0;
        }

        protected static bool? CanAddUpgrade<TGroup>(List<TGroup> groups, string shortname, DateTime lastUpdated)
            where TGroup : IUpgradeGroup
        {
            foreach (var group in groups)
            {
                var upgrades = group.Upgrades;
                if (upgrades.TryGetValue(shortname, out var currUpgrade))
                {
                    if (currUpgrade.LastUpdatedTime >= lastUpdated)
                    {
                        return false;
                    }
                    upgrades.Remove(shortname);
                    return true;
                }
            }
            return null;
        }
    }
}
