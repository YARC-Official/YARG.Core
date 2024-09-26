using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public enum ScanStage
    {
        LoadingCache,
        LoadingSongs,
        CleaningDuplicates,
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

    public abstract class CacheHandler
    {
        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 24_09_28_01;

        public static ScanProgressTracker Progress => _progress;
        private static ScanProgressTracker _progress;

        public static SongCache RunScan(bool tryQuickScan, string cacheLocation, string badSongsLocation, bool multithreading, bool allowDuplicates, bool fullDirectoryPlaylists, List<string> baseDirectories)
        {
            CacheHandler handler = multithreading
                ? new ParallelCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists)
                : new SequentialCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists);

            GlobalAudioHandler.LogMixerStatus = false;
            try
            {
                if (!tryQuickScan || !QuickScan(handler, cacheLocation))
                {
                    FullScan(handler, !tryQuickScan, cacheLocation, badSongsLocation);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Unknown error while running song scan!");
            }
            GlobalAudioHandler.LogMixerStatus = true;
            return handler.cache;
        }

        private static bool QuickScan(CacheHandler handler, string cacheLocation)
        {
            try
            {
                using var cacheFile = LoadCacheToMemory(cacheLocation, handler.fullDirectoryPlaylists);
                if (cacheFile.IsAllocated)
                {
                    _progress.Stage = ScanStage.LoadingCache;
                    YargLogger.LogDebug("Quick Read start");
                    handler.Deserialize_Quick(cacheFile.ToStream());
                }
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
            handler.SortEntries();
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);
            return true;
        }

        private static void FullScan(CacheHandler handler, bool loadCache, string cacheLocation, string badSongsLocation)
        {
            if (loadCache)
            {
                try
                {
                    using var cacheFile = LoadCacheToMemory(cacheLocation, handler.fullDirectoryPlaylists);
                    if (cacheFile.IsAllocated)
                    {
                        _progress.Stage = ScanStage.LoadingCache;
                        YargLogger.LogDebug("Full Read start");
                        handler.Deserialize(cacheFile.ToStream());
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            _progress.Stage = ScanStage.LoadingSongs;
            handler.FindNewEntries();
            handler.DisposeLeftoverData();

            _progress.Stage = ScanStage.CleaningDuplicates;
            handler.CleanupDuplicates();

            _progress.Stage = ScanStage.Sorting;
            handler.SortEntries();
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

        #region Data

        protected readonly SongCache cache = new();

        protected readonly List<IniGroup> iniGroups;
        protected readonly List<UpdateGroup> updateGroups = new();
        protected readonly List<UpgradeGroup> upgradeGroups = new();
        protected readonly List<PackedCONGroup> conGroups = new();
        protected readonly List<UnpackedCONGroup> extractedConGroups = new();
        protected readonly Dictionary<string, CONModification> conModifications = new();
        protected readonly Dictionary<string, RBProUpgrade> cacheUpgrades = new();
        protected readonly HashSet<string> preScannedDirectories = new();
        protected readonly HashSet<string> preScannedFiles = new();


        protected readonly bool allowDuplicates = true;
        protected readonly bool fullDirectoryPlaylists;
        protected readonly List<SongEntry> duplicatesRejected = new();
        protected readonly List<SongEntry> duplicatesToRemove = new();
        protected readonly SortedDictionary<string, ScanResult> badSongs = new();
        #endregion

        #region Common

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

        protected abstract void SortEntries();
        protected abstract void AddPackedCONGroup(PackedCONGroup group);
        protected abstract void AddUnpackedCONGroup(UnpackedCONGroup group);
        protected abstract void AddUpdateGroup(UpdateGroup group);
        protected abstract void AddUpgradeGroup(UpgradeGroup group);
        protected abstract void RemoveCONEntry(string shortname);
        protected abstract CONModification GetModification(string name);

        protected abstract void FindNewEntries();
        protected abstract void TraverseDirectory(in FileCollection collection, IniGroup group, PlaylistTracker tracker);

        protected abstract void Deserialize(UnmanagedMemoryStream stream);
        protected abstract void Deserialize_Quick(UnmanagedMemoryStream stream);
        protected abstract void AddCollectionToCache(in FileCollection collection);
        protected abstract PackedCONGroup? FindCONGroup(string filename);
        protected abstract void AddCacheUpgrade(string name, RBProUpgrade upgrade);

        protected virtual bool FindOrMarkDirectory(string directory)
        {
            if (!preScannedDirectories.Add(directory))
            {
                return false;
            }
            _progress.NumScannedDirectories++;
            return true;
        }

        protected virtual bool FindOrMarkFile(string file)
        {
            return preScannedFiles.Add(file);
        }
        protected virtual void AddToBadSongs(string filePath, ScanResult err)
        {
            badSongs.Add(filePath, err);
            _progress.BadSongCount++;
        }

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

        protected virtual void AddInvalidSong(string name)
        {
            invalidSongsInCache.Add(name);
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

        private void DisposeLeftoverData()
        {
            foreach (var group in conGroups)
            {
                group.UpgradeDTAData.Dispose();
            }

            foreach (var group in upgradeGroups)
            {
                group.DTAData.Dispose();
            }

            foreach (var group in updateGroups)
            {
                group.DTAData.Dispose();
            }
        }

        private void CleanupDuplicates()
        {
            foreach (var entry in duplicatesToRemove)
            {
                if (!TryRemove(iniGroups, entry) && !TryRemove(conGroups, entry))
                {
                    TryRemove(extractedConGroups, entry);
                }
            }
        }

        private static bool TryRemove<TGroup>(List<TGroup> groups, SongEntry entry)
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
                    case ScanResult.DuplicateFilesFound:
                        writer.WriteLine("Multiple sub files or directories that share the same name found in this location.");
                        writer.WriteLine("You must rename or remove all duplicates before they will be processed.");
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
                    case ScanResult.MissingCONMidi:
                        writer.WriteLine("Midi file queried for found missing");
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
                        writer.WriteLine("Loose chart files halted all traversal into the subdirectories at this location.");
                        writer.WriteLine("To fix, if desired, place the loose chart files in a separate dedicated folder.");
                        break;
                }
                writer.WriteLine();
            }
        }
        #endregion

        #region Scanning

        protected readonly struct PlaylistTracker
        {
            private readonly bool _fullDirectoryFlag;
            private readonly string? _playlist;

            public string Playlist => !string.IsNullOrEmpty(_playlist) ? _playlist : "Unknown Playlist";

            public PlaylistTracker(bool fullDirectoryFlag, string? playlist)
            {
                _fullDirectoryFlag = fullDirectoryFlag;
                _playlist = playlist;
            }

            public PlaylistTracker Append(string directory)
            {
                string playlist = string.Empty;
                if (_playlist != null)
                {
                    playlist = _fullDirectoryFlag ? Path.Combine(_playlist, directory) : directory;
                }
                return new PlaylistTracker(_fullDirectoryFlag, playlist);
            }
        }

        protected const string SONGS_DTA = "songs.dta";
        protected const string SONGUPDATES_DTA = "songs_updates.dta";
        protected const string SONGUPGRADES_DTA = "upgrades.dta";

        protected void ScanDirectory(DirectoryInfo directory, IniGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!FindOrMarkDirectory(directory.FullName) || (directory.Attributes & FileAttributes.Hidden) != 0)
                {
                    return;
                }

                if (!collectionCache.TryGetValue(directory.FullName, out var collection))
                {
                    collection = new FileCollection(directory);
                }

                if (ScanIniEntry(in collection, group, tracker.Playlist))
                {
                    if (collection.SubDirectories.Count > 0)
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                    return;
                }

                switch (directory.Name)
                {
                    case "songs_updates":
                        {
                            if (collection.Subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
                            {
                                var updateGroup = CreateUpdateGroup(in collection, dta);
                                if (updateGroup != null)
                                {
                                    foreach (var node in updateGroup.Updates)
                                    {
                                        RemoveCONEntry(node.Key);
                                    }
                                }
                                return;
                            }
                            break;
                        }
                    case "songs_upgrades":
                        {
                            if (collection.Subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
                            {
                                var upgradeGroup = CreateUpgradeGroup(in collection, dta);
                                if (upgradeGroup != null)
                                {
                                    foreach (var node in upgradeGroup.Upgrades)
                                    {
                                        RemoveCONEntry(node.Key);
                                    }
                                }
                                return;
                            }
                            break;
                        }
                    case "songs":
                        {
                            if (collection.Subfiles.TryGetValue(SONGS_DTA, out var dta))
                            {
                                var _ = CreateUnpackedCONGroup(directory.FullName, dta, tracker.Playlist);
                                return;
                            }
                            break;
                        }
                }

                TraverseDirectory(collection, group, tracker.Append(directory.Name));
                if (collection.ContainedDupes)
                {
                    AddToBadSongs(collection.Directory.FullName, ScanResult.DuplicateFilesFound);
                }
            }
            catch (PathTooLongException)
            {
                YargLogger.LogFormatError("Path {0} is too long for the file system!", directory.FullName);
                AddToBadSongs(directory.FullName, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning directory {directory.FullName}!");
            }
        }

        protected void ScanFile(FileInfo info, IniGroup group, in PlaylistTracker tracker)
        {
            string filename = info.FullName;
            try
            {
                // Ensures only fully downloaded unmarked files are processed
                if (FindOrMarkFile(filename) && (info.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) == 0)
                {
                    var abridged = new AbridgedFileInfo(info);
                    string ext = info.Extension;
                    if (ext == ".sng" || ext == ".yargsong")
                    {
                        using var sngFile = SngFile.TryLoadFromFile(abridged);
                        if (sngFile != null)
                        {
                            ScanSngFile(sngFile, group, tracker.Playlist);
                        }
                        else
                        {
                            AddToBadSongs(info.FullName, ScanResult.PossibleCorruption);
                        }
                    }
                    else
                    {
                        var conGroup = CreateCONGroup(in abridged, tracker.Playlist);
                        if (conGroup != null)
                        {
                            foreach (var node in conGroup.Upgrades)
                            {
                                RemoveCONEntry(node.Key);
                            }
                        }
                    }
                }
            }
            catch (PathTooLongException)
            {
                YargLogger.LogFormatError("Path {0} is too long for the file system!", filename);
                AddToBadSongs(filename, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning file {filename}!");
            }
        }

        protected unsafe void ScanCONNode<TGroup, TEntry>(TGroup group, string name, int index, in YARGTextContainer<byte> node, delegate*<TGroup, string, DTAEntry, CONModification, (ScanResult, TEntry?)> func)
            where TGroup : CONGroup<TEntry>
            where TEntry : RBCONEntry
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                {
                    group.RemoveEntry(name, index);
                }
            }
            else
            {
                var dtaEntry = new DTAEntry(name, in node);
                var modification = GetModification(name);
                var song = func(group, name, dtaEntry, modification);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                    {
                        group.AddEntry(name, index, song.Item2);
                    }
                }
                else
                {
                    AddToBadSongs(group.Location + $" - Node {name}", song.Item1);
                }
            }
        }

        protected void InitModification(CONModification modification, string name)
        {
            var datetime = default(DateTime);
            foreach (var group in updateGroups)
            {
                if (group.Updates.TryGetValue(name, out var update))
                {
                    if (modification.UpdateDTA == null || datetime < group.DTALastWrite)
                    {
                        modification.UpdateDTA = new DTAEntry(update.Containers[0].Encoding);
                        foreach (var container in update.Containers)
                        {
                            modification.UpdateDTA.LoadData(name, container);
                        }
                        modification.Midi = update.Midi;
                        modification.Mogg = update.Mogg;
                        modification.Milo = update.Milo;
                        modification.Image = update.Image;
                        datetime = group.DTALastWrite;
                    }
                }
            }

            foreach (var group in upgradeGroups)
            {
                if (group.Upgrades.TryGetValue(name, out var node) && node.Upgrade != null)
                {
                    if (modification.UpgradeDTA == null || datetime < group.DTALastWrite)
                    {
                        modification.UpgradeNode = node.Upgrade;
                        modification.UpgradeDTA = new DTAEntry(name, in node.Container);
                        datetime = group.DTALastWrite;
                    }
                }
            }

            foreach (var group in conGroups)
            {
                if (group.Upgrades.TryGetValue(name, out var node) && node.Upgrade != null)
                {
                    if (modification.UpgradeDTA == null || datetime < group.Info.LastUpdatedTime)
                    {
                        modification.UpgradeNode = node.Upgrade;
                        modification.UpgradeDTA = new DTAEntry(name, in node.Container);
                        datetime = group.Info.LastUpdatedTime;
                    }
                }
            }
        }

        private bool ScanIniEntry(in FileCollection collection, IniGroup group, string defaultPlaylist)
        {
            int i = collection.Subfiles.TryGetValue("song.ini", out var ini) ? 0 : 2;
            while (i < 3)
            {
                if (!collection.Subfiles.TryGetValue(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                if (!collection.ContainsAudio())
                {
                    AddToBadSongs(chart.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = UnpackedIniEntry.ProcessNewEntry(collection.Directory.FullName, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, ini, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(chart.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (PathTooLongException)
                {
                    YargLogger.LogFormatError("Path {0} is too long for the file system!", chart);
                    AddToBadSongs(chart.FullName, ScanResult.PathTooLong);
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while scanning chart file {chart}!");
                    AddToBadSongs(collection.Directory.FullName, ScanResult.IniEntryCorruption);
                }
                return true;
            }
            return false;
        }

        private void ScanSngFile(SngFile sngFile, IniGroup group, string defaultPlaylist)
        {
            int i = sngFile.Metadata.Count > 0 ? 0 : 2;
            while (i < 3)
            {
                if (!sngFile.TryGetValue(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                if (!sngFile.ContainsAudio())
                {
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = SngEntry.ProcessNewEntry(sngFile, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(sngFile.Info.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while scanning chart file {chart} within {sngFile.Info.FullName}!");
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.IniEntryCorruption);
                }
                break;
            }
        }
        #endregion

        #region Serialization

        public const int SIZEOF_DATETIME = 8;
        protected readonly HashSet<string> invalidSongsInCache = new();
        protected readonly Dictionary<string, FileCollection> collectionCache = new();

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        ///
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        private static FixedArray<byte> LoadCacheToMemory(string cacheLocation, bool fullDirectoryPlaylists)
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargLogger.LogDebug("Cache invalid or not found");
                return FixedArray<byte>.Null;
            }

            using var stream = new FileStream(cacheLocation, FileMode.Open, FileAccess.Read);
            if (stream.Read<int>(Endianness.Little) != CACHE_VERSION)
            {
                YargLogger.LogDebug($"Cache outdated");
                return FixedArray<byte>.Null;
            }

            if (stream.ReadBoolean() != fullDirectoryPlaylists)
            {
                YargLogger.LogDebug($"FullDirectoryFlag flipped");
                return FixedArray<byte>.Null;
            }
            return FixedArray<byte>.ReadRemainder(stream);
        }

        private void Serialize(string cacheLocation)
        {
            using var filestream = new FileStream(cacheLocation, FileMode.Create, FileAccess.Write);
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            filestream.Write(CACHE_VERSION, Endianness.Little);
            filestream.Write(fullDirectoryPlaylists);

            CategoryWriter.WriteToCache(filestream, cache.Titles, SongAttribute.Name, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Artists, SongAttribute.Artist, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Albums, SongAttribute.Album, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Genres, SongAttribute.Genre, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Years, SongAttribute.Year, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Charters, SongAttribute.Charter, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Playlists, SongAttribute.Playlist, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Sources, SongAttribute.Source, ref nodes);

            List<PackedCONGroup> upgradeCons = new();
            List<PackedCONGroup> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Upgrades.Count > 0)
                    upgradeCons.Add(group);

                if (group.Count > 0)
                    entryCons.Add(group);
            }

            ICacheGroup.SerializeGroups(iniGroups, filestream, nodes);
            IModificationGroup.SerializeGroups(updateGroups, filestream);
            IModificationGroup.SerializeGroups(upgradeGroups, filestream);
            IModificationGroup.SerializeGroups(upgradeCons, filestream);
            ICacheGroup.SerializeGroups(entryCons, filestream, nodes);
            ICacheGroup.SerializeGroups(extractedConGroups, filestream, nodes);
        }

        protected void ReadIniEntry(IniGroup group, string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            bool isSngEntry = stream.ReadBoolean();
            string fullname = Path.Combine(directory, stream.ReadString());

            IniSubEntry? entry = isSngEntry
                ? ReadSngEntry(fullname, stream, strings)
                : ReadUnpackedIniEntry(fullname, stream, strings);

            if (entry != null && AddEntry(entry))
            {
                group.AddEntry(entry);
            }
        }

        protected void QuickReadIniEntry(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            bool isSngEntry = stream.ReadBoolean();
            string fullname = Path.Combine(directory, stream.ReadString());

            IniSubEntry? entry = isSngEntry
                ? SngEntry.LoadFromCache_Quick(fullname, stream, strings)
                : UnpackedIniEntry.IniFromCache_Quick(fullname, stream, strings);

            if (entry != null)
            {
                AddEntry(entry);
            }
            else
            {
                YargLogger.LogError("Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
            }
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
            if (!collection.Subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
            {
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            var group = CreateUpdateGroup(in collection, dta);
            if (group != null && group.DTALastWrite == dtaLastWritten)
            {
                var updates = new Dictionary<string, SongUpdate>(group.Updates);
                for (int i = 0; i < count; i++)
                {
                    string name = stream.ReadString();
                    if (updates.Remove(name, out var update))
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

                foreach (var leftover in updates.Keys)
                {
                    AddInvalidSong(leftover);
                }
                return;
            }

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
            if (!collection.Subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
            {
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            var group = CreateUpgradeGroup(in collection, dta);
            if (group != null && dta.LastWriteTime == dtaLastWritten)
            {
                ValidateUpgrades(group.Upgrades, count, stream);
                return;
            }

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
            int count = stream.Read<int>(Endianness.Little);

            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                goto Invalidate;
            }

            var group = CreateCONGroup(filename, baseGroup.Directory);
            if (group != null && group.Info.LastUpdatedTime == conLastUpdated)
            {
                ValidateUpgrades(group.Upgrades, count, stream);
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                stream.Position += SIZEOF_DATETIME;
            }
        }

        private void ValidateUpgrades<TUpgrade>(Dictionary<string, (YARGTextContainer<byte> Container, TUpgrade Upgrade)> groupUpgrades, int count, UnmanagedMemoryStream stream)
            where TUpgrade : RBProUpgrade
        {
            var upgrades = new Dictionary<string, DateTime>();
            upgrades.EnsureCapacity(groupUpgrades.Count);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                upgrades.Add(name, lastUpdated);
            }

            foreach (var node in groupUpgrades)
            {
                if (upgrades.Remove(node.Key, out var dateTime) && node.Value.Upgrade.LastUpdatedTime == dateTime)
                {
                    AddCacheUpgrade(node.Key, node.Value.Upgrade);
                }
                else
                {
                    AddInvalidSong(node.Key);
                }
            }

            foreach (var leftover in upgrades.Keys)
            {
                AddInvalidSong(leftover);
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

            var conLastUpdate = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            var group = FindCONGroup(filename) ?? CreateCONGroup(filename, baseGroup.Directory);
            return group != null && group.Info.LastUpdatedTime == conLastUpdate ? group : null;
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
            var group = CreateUnpackedCONGroup(directory, dtaInfo, playlist);
            if (group == null)
            {
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            return dtaInfo.LastWriteTime == dtaLastWrite ? group : null;
        }

        protected void QuickReadUpgradeDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            stream.Position += sizeof(long); // Can skip the last update time
            int count = stream.Read<int>(Endianness.Little);

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                var info = new AbridgedFileInfo(filename, stream);
                AddCacheUpgrade(name, new UnpackedRBProUpgrade(info));
            }
        }

        protected void QuickReadUpgradeCON(UnmanagedMemoryStream stream)
        {
            var listings = QuickReadCONGroupHeader(stream);
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                var listing = default(CONFileListing);
                listings?.TryGetListing($"songs_upgrades/{name}_plus.mid", out listing);
                AddCacheUpgrade(name, new PackedRBProUpgrade(listing, lastWrite));
            }
        }

        protected List<CONFileListing>? QuickReadCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var info = new AbridgedFileInfo(filename, stream);
            if (File.Exists(filename))
            {
                return null;
            }

            using var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return CONFile.TryParseListings(in info, filestream);
        }

        private UnpackedIniEntry? ReadUnpackedIniEntry(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var entry = UnpackedIniEntry.TryLoadFromCache(directory, stream, strings);
            if (entry == null)
            {
                return null;
            }
            FindOrMarkDirectory(directory);
            return entry;
        }

        private SngEntry? ReadSngEntry(string fullname, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var entry = SngEntry.TryLoadFromCache(fullname, stream, strings);
            if (entry == null)
            {
                return null;
            }
            FindOrMarkFile(fullname);
            return entry;
        }

        private UpdateGroup? CreateUpdateGroup(in FileCollection collection, FileInfo dta)
        {
            UpdateGroup? group = null;
            try
            {
                using var data = FixedArray<byte>.Load(dta.FullName);
                var updates = new Dictionary<string, SongUpdate>();
                var container = YARGDTAReader.TryCreate(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    if (!updates.TryGetValue(name, out var update))
                    {
                        AbridgedFileInfo? midi = null;
                        AbridgedFileInfo? mogg = null;
                        AbridgedFileInfo? milo = null;
                        AbridgedFileInfo? image = null;

                        string subname = name.ToLowerInvariant();
                        if (collection.SubDirectories.TryGetValue(subname, out var directory))
                        {
                            string midiName = subname + "_update.mid";
                            string moggName = subname + "_update.mogg";
                            string miloName = subname + ".milo_xbox";
                            string imageName = subname + "_keep.png_xbox";
                            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                string filename = file.Name;
                                if (filename == midiName)
                                {
                                    midi = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == moggName)
                                {
                                    mogg = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == miloName)
                                {
                                    milo = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == imageName)
                                {
                                    image = new AbridgedFileInfo(file, false);
                                }
                            }

                        }
                        updates.Add(name, update = new SongUpdate(in midi, in mogg, in milo, in image));
                    }
                    update.Containers.Add(container);
                    YARGDTAReader.EndNode(ref container);
                }

                if (updates.Count > 0)
                {
                    group = new UpdateGroup(collection.Directory.FullName, dta.LastWriteTime, data.TransferOwnership(), updates);
                    AddUpdateGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return group;
        }

        private UpgradeGroup? CreateUpgradeGroup(in FileCollection collection, FileInfo dta)
        {
            UpgradeGroup? group = null;
            try
            {
                using var data = FixedArray<byte>.Load(dta.FullName);
                var upgrades = new Dictionary<string, (YARGTextContainer<byte> Container, UnpackedRBProUpgrade Upgrade)>();
                var container = YARGDTAReader.TryCreate(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    var upgrade = default(UnpackedRBProUpgrade);
                    if (collection.Subfiles.TryGetValue($"{name.ToLower()}_plus.mid", out var info))
                    {
                        var abridged = new AbridgedFileInfo(info, false);
                        upgrade = new UnpackedRBProUpgrade(abridged);
                        upgrades[name] = (container, upgrade);
                    }
                    YARGDTAReader.EndNode(ref container);
                }

                if (upgrades.Count > 0)
                {
                    group = new UpgradeGroup(collection.Directory.FullName, dta.LastWriteTime, data.TransferOwnership(), upgrades);
                    AddUpgradeGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return group;
        }

        private PackedCONGroup? CreateCONGroup(string filename, string baseDirectory)
        {
            var info = new FileInfo(filename);
            if (!info.Exists)
            {
                return null;
            }

            FindOrMarkFile(filename);

            string playlist = ConstructPlaylist(filename, baseDirectory);
            var abridged = new AbridgedFileInfo(info);
            return CreateCONGroup(in abridged, playlist);
        }

        private PackedCONGroup? CreateCONGroup(in AbridgedFileInfo info, string defaultPlaylist)
        {
            const string SONGSFILEPATH = "songs/songs.dta";
            const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";
            PackedCONGroup? group = null;
            // Holds the file that caused an error in some form
            string errorFile = string.Empty;
            try
            {
                using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                var listings = CONFile.TryParseListings(in info, stream);
                if (listings == null)
                {
                    return null;
                }

                var songNodes = new Dictionary<string, List<YARGTextContainer<byte>>>();
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var songDTAData = listings.TryGetListing(SONGSFILEPATH, out var songDTA) ? songDTA.LoadAllBytes(stream) : FixedArray<byte>.Null;
                if (songDTAData.IsAllocated)
                {
                    errorFile = SONGSFILEPATH;
                    var container = YARGDTAReader.TryCreate(songDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (!songNodes.TryGetValue(name, out var list))
                        {
                            songNodes.Add(name, list = new List<YARGTextContainer<byte>>());
                        }
                        list.Add(container);
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                var upgrades = new Dictionary<string, (YARGTextContainer<byte> Container, PackedRBProUpgrade Upgrade)>();
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var upgradeDTAData = listings.TryGetListing(UPGRADESFILEPATH, out var upgradeDta) ? upgradeDta.LoadAllBytes(stream) : FixedArray<byte>.Null;
                if (upgradeDTAData.IsAllocated)
                {
                    errorFile = UPGRADESFILEPATH;
                    var container = YARGDTAReader.TryCreate(upgradeDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (listings.TryGetListing($"songs_upgrades/{name}_plus.mid", out var listing))
                        {
                            var upgrade = new PackedRBProUpgrade(listing, listing.LastWrite);
                            upgrades[name] = (container, upgrade);
                        }
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                if (songNodes.Count > 0 || upgrades.Count > 0)
                {
                    group = new PackedCONGroup(listings, songDTAData.TransferOwnership(), upgradeDTAData.TransferOwnership(), songNodes, upgrades, in info, defaultPlaylist);
                    AddPackedCONGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {errorFile}");
            }
            return group;
        }

        private UnpackedCONGroup? CreateUnpackedCONGroup(string directory, FileInfo dta, string defaultPlaylist)
        {
            try
            {
                using var songDTAData = FixedArray<byte>.Load(dta.FullName);

                var songNodes = new Dictionary<string, List<YARGTextContainer<byte>>>();
                if (songDTAData.IsAllocated)
                {
                    var container = YARGDTAReader.TryCreate(songDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (!songNodes.TryGetValue(name, out var list))
                        {
                            songNodes.Add(name, list = new List<YARGTextContainer<byte>>());
                        }
                        list.Add(container);
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                if (songNodes.Count > 0)
                {
                    var abridged = new AbridgedFileInfo(dta);
                    var group = new UnpackedCONGroup(songDTAData.TransferOwnership(), songNodes, directory, in abridged, defaultPlaylist);
                    AddUnpackedCONGroup(group);
                    return group;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return null;
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
        #endregion
    }

    internal static class UnmanagedStreamSlicer
    {
        public static unsafe UnmanagedMemoryStream Slice(this UnmanagedMemoryStream stream, int length)
        {
            if (stream.Position > stream.Length - length)
            {
                throw new EndOfStreamException();
            }

            var newStream = new UnmanagedMemoryStream(stream.PositionPointer, length);
            stream.Position += length;
            return newStream;
        }
    }

    internal static class AudioFinder
    {
        public static bool ContainsAudio(this SngFile sngFile)
        {
            foreach (var file in sngFile)
            {
                if (IniAudio.IsAudioFile(file.Key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
