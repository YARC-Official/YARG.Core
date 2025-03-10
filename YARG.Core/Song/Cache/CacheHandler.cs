using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    public class CacheHandler : IDisposable
    {
        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        private const int CACHE_VERSION = 25_03_08_02;

        public static ScanProgressTracker Progress => _progress;
        private static ScanProgressTracker _progress;

        public static SongCache RunScan(bool tryQuickScan, string cacheLocation, string badSongsLocation, bool fullDirectoryPlaylists, List<string> baseDirectories)
        {
            using var handler = new CacheHandler(baseDirectories);

            // Some ini entry items won't come with the song length defined in the .ini file.
            // In those instances, we'll need to attempt to load the audio files that accompany the chart
            // to evaluate the length directly.
            // This toggle simply keeps those generated mixers from spamming the logs on creation.
            GlobalAudioHandler.LogMixerStatus = false;
            try
            {
                // Quick scans only fail if they parse zero entries (which could be the result of a few things)
                if (!tryQuickScan || !QuickScan(handler, cacheLocation, fullDirectoryPlaylists))
                {
                    // If a quick scan failed, there's no point to re-reading it in the full scan
                    FullScan(handler, !tryQuickScan, cacheLocation, badSongsLocation, fullDirectoryPlaylists);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Unknown error while running song scan!");
            }
            GlobalAudioHandler.LogMixerStatus = true;
            return handler.cache;
        }

        /// <summary>
        /// Reads the entries from a cache file - performing very few validation checks on the entries contained within
        /// for the sole purpose of speeding through to gameplay.
        /// </summary>
        /// <param name="handler">A parallel or sequential handler</param>
        /// <param name="cacheLocation">File path of the cache</param>
        /// <returns>Whether the scan sucessfully parsed entries</returns>
        private static bool QuickScan(CacheHandler handler, string cacheLocation, bool fullDirectoryPlaylists)
        {
            try
            {
                using var cacheFile = LoadCacheToMemory(cacheLocation, fullDirectoryPlaylists);
                if (cacheFile.IsAllocated)
                {
                    _progress.Stage = ScanStage.LoadingCache;
                    YargLogger.LogDebug("Quick Read start");
                    handler.Deserialize_Quick(in cacheFile);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error occurred during quick cache file read!");
            }

            if (handler.cache.Entries.Count == 0)
            {
                return false;
            }

            _progress.Stage = ScanStage.Sorting;
            SongEntrySorting.SortEntries(handler.cache);
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);
            return true;
        }

        /// <summary>
        /// Runs a full scan process for a user's library.
        /// Firstly, it attempts to read entries from a cache file - performing all validation checks necessary
        /// to ensure that the player can immediately play whatever comes off the cache.
        /// Secondly, we traverse the user's filesystem starting from their provided base directory nodes for any entries
        /// that were not found from the cache or required re-evaluating.
        /// Finally, we write the results of the scan back to a cache file and, if necessary, a badsongs.txt file containing the failures.
        /// </summary>
        /// <param name="handler">A parallel or sequential handler</param>
        /// <param name="loadCache">A flag communicating whether to perform the cache read (false only from failed quick scans)</param>
        /// <param name="cacheLocation">File path of the cache</param>
        /// <param name="badSongsLocation">File path of the badsongs.txt</param>
        private static void FullScan(CacheHandler handler, bool loadCache, string cacheLocation, string badSongsLocation, bool fullDirectoryPlaylists)
        {
            if (loadCache)
            {
                try
                {
                    using var cacheFile = LoadCacheToMemory(cacheLocation, fullDirectoryPlaylists);
                    if (cacheFile.IsAllocated)
                    {
                        _progress.Stage = ScanStage.LoadingCache;
                        YargLogger.LogDebug("Full Read start");
                        handler.Deserialize(in cacheFile, fullDirectoryPlaylists);
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            _progress.Stage = ScanStage.LoadingSongs;
            handler.FindNewEntries(fullDirectoryPlaylists);
            // CON, Upgrade, and Update groups hold onto the DTA data in memory.
            // Once all entries are processed, they are no longer useful to us, so we dispose of them here.
            handler.Dispose();

            _progress.Stage = ScanStage.Sorting;
            SongEntrySorting.SortEntries(handler.cache);
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);

            try
            {
                _progress.Stage = ScanStage.WritingCache;
                handler.Serialize(cacheLocation, fullDirectoryPlaylists);
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

        private readonly SongCache cache = new();

        private readonly List<IniEntryGroup> iniGroups;
        private readonly List<CONEntryGroup> conEntryGroups = new();
        private readonly List<CONUpdateGroup> updateGroups = new();
        private readonly List<PackedCONUpgradeGroup> packedUpgradeGroups = new();
        private readonly List<UnpackedCONUpgradeGroup> unpackedUpgradeGroups = new();

        private readonly Dictionary<string, CONModification> conModifications = new();

        private readonly HashSet<string> preScannedPaths = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();
        #endregion

        #region Common

        private CacheHandler(List<string> baseDirectories)
        {
            _progress = default;

            iniGroups = new(baseDirectories.Count);
            foreach (string dir in baseDirectories)
            {
                if (!string.IsNullOrEmpty(dir) && !iniGroups.Exists(group => { return group.Directory == dir; }))
                {
                    iniGroups.Add(new IniEntryGroup(dir));
                }
            }
        }

        /// <summary>
        /// Removes all the entries present in all packed and unpacked con groups that have a matching DTA node name
        /// </summary>
        private void RemoveCONEntries<T>(Dictionary<string, T> modifications)
        {
            lock (conEntryGroups)
            {
                foreach (var mod in modifications)
                {
                    for (int i = 0; i < conEntryGroups.Count; i++)
                    {
                        conEntryGroups[i].RemoveEntries(mod.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Grabs or constructs a node containing all the updates or upgrades that can applied to any DTA entries
        /// that have a name matching the one provided.
        /// </summary>
        /// <param name="name">The name of the DTA node for the entry</param>
        /// <returns>The node with the update and upgrade information</returns>
        private CONModification GetCONMod(string name)
        {
            CONModification mods;
            lock (conModifications)
            {
                if (!conModifications.TryGetValue(name, out mods))
                {
                    conModifications.Add(name, mods = new CONModification());
                }
            }

            lock (mods)
            {
                if (!mods.Processed)
                {
                    foreach (var group in updateGroups)
                    {
                        if (!group.Updates.TryGetValue(name, out var node))
                        {
                            continue;
                        }

                        if (mods.UpdateDirectoryAndDtaLastWrite.HasValue &&
                            group.Root.LastWriteTime <= mods.UpdateDirectoryAndDtaLastWrite.Value.LastWriteTime)
                        {
                            continue;
                        }

                        mods.UpdateDirectoryAndDtaLastWrite = group.Root;
                        mods.UpdateDTA = DTAEntry.Empty;
                        for (int i = 0; i < node.Containers.Count; ++i)
                        {
                            mods.UpdateDTA.LoadData(name, node.Containers[i]);
                        }

                        mods.UpdateMidiLastWrite = node.Update;
                        if (!mods.UpdateMidiLastWrite.HasValue && mods.UpdateDTA.DiscUpdate)
                        {
                            YargLogger.LogFormatWarning("Update midi expected in directory {0}", Path.Combine(group.Root.FullName, name));
                        }
                    }

                    foreach (var group in packedUpgradeGroups)
                    {
                        if (!group.Upgrades.TryGetValue(name, out var node))
                        {
                            continue;
                        }

                        if (mods.Upgrade != null && node.Upgrade.LastWriteTime <= mods.Upgrade.LastWriteTime)
                        {
                            continue;
                        }

                        mods.Upgrade = node.Upgrade;
                        mods.UpgradeDTA = DTAEntry.Create(name, node.Container);
                    }

                    foreach (var group in unpackedUpgradeGroups)
                    {
                        if (!group.Upgrades.TryGetValue(name, out var node))
                        {
                            continue;
                        }

                        if (mods.Upgrade != null && node.Upgrade.LastWriteTime <= mods.Upgrade.LastWriteTime)
                        {
                            continue;
                        }

                        mods.Upgrade = node.Upgrade;
                        mods.UpgradeDTA = DTAEntry.Create(name, node.Container);
                    }
                    mods.Processed = true;
                }
            }
            return mods;
        }

        /// <summary>
        /// Performs the traversal of the filesystem in search of new entries to add to a user's library
        /// </summary>
        private void FindNewEntries(bool fullDirectoryPlaylists)
        {
            var tracker = new PlaylistTracker(fullDirectoryPlaylists, null);
            Parallel.ForEach(iniGroups, group =>
            {
                var dirInfo = new DirectoryInfo(group.Directory);
                ScanDirectory(dirInfo, group, tracker);
            });

            Parallel.ForEach(conEntryGroups, group =>
            {
                group.InitScan();
                Parallel.ForEach(group, node =>
                {
                    var mods = GetCONMod(node.Key);
                    if (mods.UpdateDirectoryAndDtaLastWrite != null)
                    {
                        string moggPath = Path.Combine(mods.UpdateDirectoryAndDtaLastWrite.Value.FullName, node.Key, node.Key + ".mogg");
                        if (File.Exists(moggPath))
                        {
                            try
                            {
                                using var stream = new FileStream(moggPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                                if (stream.Read<int>(Endianness.Little) != RBCONEntry.UNENCRYPTED_MOGG)
                                {
                                    AddToBadSongs(group.Root.FullName + " - " + node.Key, ScanResult.MoggError_Update);
                                    return;
                                }
                            }
                            catch (Exception e)
                            {
                                YargLogger.LogException(e);
                                AddToBadSongs(group.Root.FullName + " - " + node.Key, ScanResult.MoggError_Update);
                                return;
                            }
                        }
                    }

                    var parameters = new RBScanParameters()
                    {
                        UpdateDta = mods.UpdateDTA,
                        UpgradeDta = mods.UpgradeDTA,
                        Root = group.Root,
                        NodeName = node.Key,
                        UpdateDirectory = mods.UpdateDirectoryAndDtaLastWrite,
                        UpdateMidi = mods.UpdateMidiLastWrite,
                        Upgrade = mods.Upgrade,
                        DefaultPlaylist = group.DefaultPlaylist,
                    };

                    for (int i = 0; i < node.Value.Count; i++)
                    {
                        if (!group.TryGetEntry(node.Key, i, out var entry))
                        {
                            try
                            {
                                parameters.BaseDta = DTAEntry.Create(node.Key, node.Value[i]);
                                var result = group.CreateEntry(in parameters);
                                if (!result)
                                {
                                    AddToBadSongs(group.Root.FullName + " - " + node.Key, result.Error);
                                    continue;
                                }
                                group.AddEntry(node.Key, i, result.Value);
                                AddEntry(result.Value);
                            }
                            catch (Exception e)
                            {
                                YargLogger.LogException(e);
                            }
                        }
                        else
                        {
                            entry.UpdateInfo(in parameters.UpdateDirectory, in parameters.UpdateMidi, parameters.Upgrade);
                            AddEntry(entry);
                        }
                    }
                });
                group.Dispose();
            });
        }

        /// <summary>
        /// Attempts to mark a directory as "processed"
        /// </summary>
        /// <param name="directory">The directory to mark</param>
        /// <returns><see langword="true"/> if the directory was not previously marked</returns>
        private bool FindOrMarkDirectory(string directory)
        {
            lock (preScannedPaths)
            {
                if (!preScannedPaths.Add(directory))
                {
                    return false;
                }
                _progress.NumScannedDirectories++;
            }
            return true;
        }

        /// <summary>
        /// Attempts to mark a file as "processed"
        /// </summary>
        /// <param name="file">The file to mark</param>
        /// <returns><see langword="true"/> if the file was not previously marked</returns>
        private bool FindOrMarkFile(string file)
        {
            lock (preScannedPaths)
            {
                return preScannedPaths.Add(file);
            }
        }

        /// <summary>
        /// Adds an instance of a bad song
        /// </summary>
        /// <param name="filePath">The file that produced the error</param>
        /// <param name="err">The error produced</param>
        private void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badSongs)
            {
                badSongs.Add(filePath, err);
                _progress.BadSongCount++;
            }
        }

        /// <summary>
        /// Attempts to add a new entry to current list. If duplicates are allowed, this will always return true.
        /// If they are disallowed, then this will only succeed if the entry is not a duplicate or if it
        /// takes precedence over the entry currently in its place (based on a variety of factors)
        /// </summary>
        /// <param name="entry">The entry to add</param>
        /// <returns>Whether the song was accepted into the list</returns>
        private void AddEntry(SongEntry entry)
        {
            static bool ShouldReplaceFront(SongEntry replacment, SongEntry entryToReplace)
            {
                if (replacment.SubType != entryToReplace.SubType)
                {
                    return replacment.SubType > entryToReplace.SubType;
                }
                return SongEntrySorting.CompareMetadata(replacment, entryToReplace);
            }

            List<SongEntry> list;
            lock (cache.Entries)
            {
                if (!cache.Entries.TryGetValue(entry.Hash, out list))
                {
                    // Most entries will accompany unique hashes, so we can usually safely reserve just a single slot
                    cache.Entries.Add(entry.Hash, list = new List<SongEntry>(1));
                }
            }

            lock (list)
            {
                int index = 0;
                if (list.Count > 0)
                {
                    if (!ShouldReplaceFront(entry, list[0]))
                    {
                        entry.MarkAsDuplicate();
                        index = list.Count;
                    }
                    else
                    {
                        list[0].MarkAsDuplicate();
                    }
                }
                list.Insert(index, entry);
            }
            Interlocked.Increment(ref _progress.Count);
        }

        /// <summary>
        /// Disposes all DTA FixedArray data present in upgrade and update nodes.
        /// The songDTA arrays will already have been disposed of before reaching this point.
        /// </summary>
        public void Dispose()
        {
            foreach (var group in updateGroups)
            {
                group.Dispose();
            }

            foreach (var group in unpackedUpgradeGroups)
            {
                group.Dispose();
            }

            foreach (var group in packedUpgradeGroups)
            {
                group.Dispose();
            }
        }

        /// <summary>
        /// Writes all bad song instances to a badsongs.txt file for the user
        /// </summary>
        /// <param name="badSongsLocation">The path for the file</param>
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
                    case ScanResult.InvalidResolution:
                        writer.WriteLine("This chart uses an invalid resolution (or possibly contains it in an improper format, if .chart)");
                        break;
                    case ScanResult.InvalidResolution_Update:
                        writer.WriteLine("The midi chart update file applicable with this chart has an invalid resolution of zero");
                        break;
                    case ScanResult.InvalidResolution_Upgrade:
                        writer.WriteLine("The midi pro guitar upgrade file applicable with this chart has an invalid resolution of zero");
                        break;
                }
                writer.WriteLine();
            }
        }
        #endregion

        #region Scanning

        private readonly struct PlaylistTracker
        {
            private readonly bool _fullDirectoryFlag;
            // We use `null` as the default state to grant two levels of subdirectories before
            // supplying directories as the actual playlist (null -> empty -> directory)
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

        /// <summary>
        /// Checks for the presence of files pertaining to an unpacked ini entry or whether the directory
        /// is to be used for CON updates, upgrades, or extracted CON song entries.
        /// If none of those, this will further traverse through any of the subdirectories present in this directory
        /// and process all the subfiles for potential CONs or SNGs.
        /// </summary>
        /// <param name="directory">The directory instance to load and scan through</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="tracker">A tracker used to apply provide entries with default playlists</param>
        private void ScanDirectory(DirectoryInfo directory, IniEntryGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!FindOrMarkDirectory(directory.FullName))
                {
                    return;
                }

                switch (directory.Name)
                {
                    case "songs_updates":
                    {
                        var dta = new FileInfo(Path.Combine(directory.FullName, RBCONEntry.SONGUPDATES_DTA));
                        if (dta.Exists && CONUpdateGroup.Create(directory.FullName, dta, out var updateGroup))
                        {
                            lock (updateGroups)
                            {
                                updateGroups.Add(updateGroup);
                            }
                            // Ensures any con entries pulled from cache are removed for re-evaluation
                            RemoveCONEntries(updateGroup!.Updates);
                        }
                        return;
                    }
                    // A missing dta file means that we will treat the folder like any other subdirectory.
                    // It's likely that directories of this name do not denote CON entires, so that's necessary.
                    case "songs":
                    {
                        var dta = new FileInfo(Path.Combine(directory.FullName, CONEntryGroup.SONGS_DTA));
                        if (dta.Exists)
                        {
                            if (UnpackedCONEntryGroup.Create(directory.FullName, dta, tracker.Playlist, out var entryGroup))
                            {
                                lock (conEntryGroups)
                                {
                                    conEntryGroups.Add(entryGroup);
                                }
                            }
                            else
                            {
                                AddToBadSongs(directory.FullName, ScanResult.DirectoryError);
                            }
                            return;
                        }
                        break;
                    }
                }

                if (!collectionCache.TryGetValue(directory.FullName, out var collection))
                {
                    collection = new FileCollection(directory);
                }

                // Only possible on UNIX-based systems where file names are case-sensitive
                if (collection.ContainedDupes)
                {
                    AddToBadSongs(collection.Directory, ScanResult.DuplicateFilesFound);
                }

                if (directory.Name == "songs_upgrades")
                {
                    if (collection.FindFile(RBProUpgrade.UPGRADES_DTA, out var dta)
                    && UnpackedCONUpgradeGroup.Create(collection, dta, out var upgradeGroup))
                    {
                        lock (unpackedUpgradeGroups)
                        {
                            unpackedUpgradeGroups.Add(upgradeGroup);
                        }
                        // Ensures any con entries pulled from cache are removed for re-evaluation
                        RemoveCONEntries(upgradeGroup!.Upgrades);
                    }
                }
                // If we discover any combo of valid unpacked ini entry files in this directory,
                // we will traverse none of the subdirectories present in this scope
                else if (ScanIniEntry(in collection, group, tracker.Playlist))
                {
                    // However, the presence of subdirectories could mean that the user didn't properly
                    // organize their collection. So as a service, we warn them in the badsongs.txt.
                    if (collection.ContainsDirectory())
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                }
                else
                {
                    var nextTracker = tracker.Append(directory.Name);
                    Parallel.ForEach(collection, entry =>
                    {
                        switch (entry.Value)
                        {
                            case DirectoryInfo directory:
                                ScanDirectory(directory, group, nextTracker);
                                break;
                            case FileInfo file:
                                ScanFile(file, group, nextTracker);
                                break;
                        }
                    });
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

        /// <summary>
        /// Attempts to process the provided file as either a CON or SNG
        /// </summary>
        /// <param name="info">The info for provided file</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="tracker">A tracker used to apply provide entries with default playlists</param>
        private void ScanFile(FileInfo info, IniEntryGroup group, in PlaylistTracker tracker)
        {
            string filename = info.FullName;
            try
            {
                // Ensures only fully downloaded unmarked files are processed
                if (FindOrMarkFile(filename))
                {
                    string ext = info.Extension;
                    if (ext == ".sng" || ext == ".yargsong")
                    {
                        using var sngFile = SngFile.TryLoadFromFile(info.FullName, true);
                        if (sngFile.IsLoaded)
                        {
                            ScanSngFile(in sngFile, info, group, tracker.Playlist);
                        }
                        else
                        {
                            AddToBadSongs(info.FullName, ScanResult.PossibleCorruption);
                        }
                    }
                    else
                    {
                        var result = CreateCONGroup(info, tracker.Playlist);
                        if (result.Upgrades != null)
                        {
                            // Ensures any con entries pulled from cache are removed for re-evaluation
                            RemoveCONEntries(result.Upgrades.Upgrades);
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

        /// <summary>
        /// Searches for a ".ini" and any .mid or .chart file to possibly extract as a song entry.
        /// If found, even if we can't extract an entry from them, we should perform no further directory traversal.
        /// </summary>
        /// <param name="collection">The collection containing the subfiles to search from</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="defaultPlaylist">The default directory-based playlist to use for any successful entry</param>
        /// <returns>Whether files pertaining to an unpacked ini entry were discovered</returns>
        private bool ScanIniEntry(in FileCollection collection, IniEntryGroup group, string defaultPlaylist)
        {
            int i = collection.FindFile("song.ini", out var ini) ? 0 : 2;
            while (i < 3)
            {
                if (!collection.FindFile(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                // Can't play a song without any audio can you?
                //
                // Note though that this is purely a pre-add check.
                // We will not invalidate an entry from cache if the user removes the audio after the fact.
                if (!collection.ContainsAudio())
                {
                    AddToBadSongs(chart.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = UnpackedIniEntry.ProcessNewEntry(collection.Directory, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, ini, defaultPlaylist);
                    if (entry)
                    {
                        AddEntry(entry.Value);
                        group.AddEntry(entry.Value);
                    }
                    else
                    {
                        AddToBadSongs(chart.FullName, entry.Error);
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
                    AddToBadSongs(collection.Directory, ScanResult.IniEntryCorruption);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Searches for any .mid or .chart file to possibly extract as a song entry.
        /// </summary>
        /// <param name="sngFile">The sngfile to search through</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="defaultPlaylist">The default directory-based playlist to use for any successful entry</param>
        private void ScanSngFile(in SngFile sngFile, FileInfo info, IniEntryGroup group, string defaultPlaylist)
        {
            int i = !sngFile.Modifiers.IsEmpty() ? 0 : 2;
            while (i < 3)
            {
                if (!sngFile.TryGetListing(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                if (!sngFile.ContainsAudio())
                {
                    AddToBadSongs(info.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = SngEntry.ProcessNewEntry(in sngFile, in chart, info, IniSubEntry.CHART_FILE_TYPES[i].Format, defaultPlaylist);
                    if (entry.HasValue)
                    {
                        AddEntry(entry.Value);
                        group.AddEntry(entry.Value);
                    }
                    else
                    {
                        AddToBadSongs(info.FullName, entry.Error);
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while scanning chart file {chart} within {info.FullName}!");
                    AddToBadSongs(info.FullName, ScanResult.IniEntryCorruption);
                }
                break;
            }
        }
        #endregion

        #region Serialization

        public const int SIZEOF_DATETIME = 8;
        private HashSet<string> invalidSongsInCache = new();
        private Dictionary<string, FileCollection> collectionCache = new();
        private Dictionary<string, QuickCONMods> cacheCONModifications = new();
        private Dictionary<string, List<CONFileListing>?> cacheCONListings = new();

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        ///
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        /// <summary>
        /// Attempts to laod the cache file's data into a FixedArray. This will fail if an error is thrown,
        /// the cache is outdated, or if the the "full playlist" toggle mismatches.
        /// </summary>
        /// <param name="cacheLocation">File location for the cache</param>
        /// <param name="fullDirectoryPlaylists">Toggle for the display style of directory-based playlists</param>
        /// <returns>A FixedArray instance pointing to a buffer of the cache file's data, or <see cref="FixedArray&lt;&rt;"/>.Null if invalid</returns>
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
            return FixedArray.ReadRemainder(stream);
        }

        /// <summary>
        /// Serializes the cache to a file, duhhhhhhh
        /// </summary>
        /// <param name="cacheLocation">Location to save to</param>
        private void Serialize(string cacheLocation, bool fullDirectoryPlaylists)
        {
            using var filestream = new FileStream(cacheLocation, FileMode.Create, FileAccess.Write);

            filestream.Write(CACHE_VERSION, Endianness.Little);
            filestream.Write(fullDirectoryPlaylists);

            Dictionary<SongEntry, CacheWriteIndices> nodes = new();
            SongEntrySorting.WriteCategoriesToCache(filestream, cache, nodes);
            CONUpdateGroup.SerializeGroups(filestream, updateGroups);
            UnpackedCONUpgradeGroup.SerializeGroups(filestream, unpackedUpgradeGroups);
            PackedCONUpgradeGroup.SerializeGroups(filestream, packedUpgradeGroups);
            IEntryGroup.SerializeGroups(filestream, iniGroups, nodes);
            IEntryGroup.SerializeGroups(filestream, conEntryGroups, nodes);
        }

        /// <summary>
        /// Deserializes a cache file into the separate song entries with all validation checks
        /// </summary>
        /// <param name="stream">The stream containging the cache file data</param>
        private unsafe void Deserialize(in FixedArray<byte> data, bool fullDirectoryPlaylists)
        {
            var stream = data.ToValueStream();
            var strings = new CacheReadStrings(&stream);
            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                ReadUpdateDirectory(node.Slice);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                ReadUpgradeDirectory(node.Slice);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                ReadUpgradeCON(node.Slice, fullDirectoryPlaylists);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                ReadIniDirectory(node.Slice, strings);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                ReadCONGroup(node.Slice, strings, fullDirectoryPlaylists);
            });
        }

        /// <summary>
        /// Deserializes a cache file into the separate song entries with minimal validations
        /// </summary>
        /// <param name="stream">The stream containging the cache file data</param>
        private unsafe void Deserialize_Quick(in FixedArray<byte> data)
        {
            var stream = data.ToValueStream();
            var strings = new CacheReadStrings(&stream);
            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                QuickReadUpdateDirectory(node.Slice);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                QuickReadUpgradeDirectory(node.Slice);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                QuickReadUpgradeCON(node.Slice);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                QuickReadIniDirectory(node.Slice, strings);
            });

            Parallel.ForEach(new CacheLoopable(&stream), node =>
            {
                QuickReadCONGroup(node.Slice, strings);
            });
        }

        /// <summary>
        /// Reads a section of the cache containing a list of updates to apply from a specific directory,
        /// performing validations on each update node. If an update node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of updates</param>
        private void ReadUpdateDirectory(FixedArrayStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (!GetBaseIniGroup(directory, out var _))
            {
                goto Invalidate;
            }

            var dtaInfo = new FileInfo(Path.Combine(directory, RBCONEntry.SONGUPDATES_DTA));
            if (!dtaInfo.Exists)
            {
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            // Will add the update group to the shared list on success
            if (CONUpdateGroup.Create(directory, dtaInfo, out var group) && group.Root.LastWriteTime == dtaLastWrite)
            {
                lock (updateGroups)
                {
                    updateGroups.Add(group);
                }
                // We need to compare what we have on the filesystem against what's written one by one
                var songsToInvalidate = new Dictionary<string, DateTime?>();
                songsToInvalidate.EnsureCapacity(group.Updates.Count);
                foreach (var update in group.Updates)
                {
                    songsToInvalidate.Add(update.Key, update.Value.Update);
                }

                for (int i = 0; i < count; i++)
                {
                    string name = stream.ReadString();
                    DateTime? lastWrite = null;
                    if (stream.ReadBoolean())
                    {
                        lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                    }

                    if (songsToInvalidate.TryGetValue(name, out var currLastWrite))
                    {
                        if (lastWrite.HasValue == currLastWrite.HasValue)
                        {
                            if (!lastWrite.HasValue || lastWrite.Value == currLastWrite!.Value)
                            {
                                songsToInvalidate.Remove(name);
                            }
                        }
                    }
                    else
                    {
                        AddInvalidSong(name);
                    }
                }

                // Anything left in the dictionary may require invalidation of cached entries
                foreach (var leftover in songsToInvalidate)
                {
                    AddInvalidSong(leftover.Key);
                }
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                if (stream.ReadBoolean())
                {
                    stream.Position += SIZEOF_DATETIME;
                }
            }
        }

        /// <summary>
        /// Loads all the upgrade nodes present in the cache from an "upgrades folder" section
        /// </summary>
        /// <param name="stream">Stream containing the data for a folder's upgrade nodes</param>
        private void QuickReadUpdateDirectory(FixedArrayStream stream)
        {
            var root = new AbridgedFileInfo(ref stream);
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                DateTime? midiLastWrite = default;
                if (stream.ReadBoolean())
                {
                    midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                }

                var mods = GetQuickCONMods(name);
                lock (mods)
                {
                    if (!mods.UpdateDirectoryAndDtaLastWrite.HasValue || mods.UpdateDirectoryAndDtaLastWrite.Value.LastWriteTime < root.LastWriteTime)
                    {
                        mods.UpdateDirectoryAndDtaLastWrite = root;
                        mods.UpdateMidi = midiLastWrite;
                    }
                }
            }
        }

        /// <summary>
        /// Reads a section of the cache containing a list of upgrades to apply from a specific directory,
        /// performing validations on each upgrade node. If an upgrade node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of upgrades</param>
        private void ReadUpgradeDirectory(FixedArrayStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (!GetBaseIniGroup(directory, out var _))
            {
                goto Invalidate;
            }

            var dirInfo = new DirectoryInfo(directory);
            if (!dirInfo.Exists)
            {
                goto Invalidate;
            }

            var collection = new FileCollection(dirInfo);
            if (!collection.FindFile(RBProUpgrade.UPGRADES_DTA, out var dta))
            {
                // We don't *mark* the directory to allow the "New Entries" process
                // to access this collection
                lock (collectionCache)
                {
                    collectionCache.Add(directory, collection);
                }
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            if (UnpackedCONUpgradeGroup.Create(in collection, dta, out var group))
            {
                lock (unpackedUpgradeGroups)
                {
                    unpackedUpgradeGroups.Add(group);
                }

                if (dta.LastWriteTime == dtaLastWritten)
                {
                    var songsToInvalidate = new Dictionary<string, DateTime>();
                    songsToInvalidate.EnsureCapacity(group.Upgrades.Count);
                    foreach (var node in group.Upgrades)
                    {
                        songsToInvalidate.Add(node.Key, node.Value.Upgrade.LastWriteTime);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        string name = stream.ReadString();
                        if (songsToInvalidate.TryGetValue(name, out var upgradeLastWrite))
                        {
                            var midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                            if (upgradeLastWrite == midiLastWrite)
                            {
                                songsToInvalidate.Remove(name);
                            }
                        }
                        else
                        {
                            AddInvalidSong(name);
                            stream.Position += SIZEOF_DATETIME;
                        }
                    }

                    // Anything left in the dictionary may require invalidation of cached entries
                    foreach (var leftover in songsToInvalidate)
                    {
                        AddInvalidSong(leftover.Key);
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

        /// <summary>
        /// Loads all the upgrade nodes present in the cache from an "upgrades folder" section
        /// </summary>
        /// <param name="stream">Stream containing the data for a folder's upgrade nodes</param>
        private void QuickReadUpgradeDirectory(FixedArrayStream stream)
        {
            var root = new AbridgedFileInfo(ref stream);
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var midiLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                var mods = GetQuickCONMods(name);
                lock (mods)
                {
                    if (mods.Upgrade == null || mods.Upgrade.LastWriteTime < midiLastWrite)
                    {
                        mods.Upgrade = new UnpackedRBProUpgrade(name, midiLastWrite, in root);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a section of the cache containing a list of upgrades to apply from a packed CON file,
        /// performing validations on each upgrade node. If an upgrade node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of upgrades</param>
        private void ReadUpgradeCON(FixedArrayStream stream, bool fullDirectoryPlaylists)
        {
            string filename = stream.ReadString();
            var conLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            if (!GetBaseIniGroup(filename, out var baseGroup))
            {
                goto Invalidate;
            }

            var info = new FileInfo(filename);
            if (!info.Exists)
            {
                goto Invalidate;
            }

            FindOrMarkFile(filename);

            string defaultPlaylist = ConstructPlaylist(filename, baseGroup.Directory, fullDirectoryPlaylists);
            var result = CreateCONGroup(info, defaultPlaylist);
            if (result.Upgrades != null && result.Upgrades.Root.LastWriteTime == conLastWrite)
            {
                var songsToInvalidate = new HashSet<string>();
                songsToInvalidate.EnsureCapacity(result.Upgrades.Upgrades.Count);
                foreach (var node in result.Upgrades.Upgrades)
                {
                    songsToInvalidate.Add(node.Key);
                }

                for (int i = 0; i < count; i++)
                {
                    string name = stream.ReadString();
                    if (!songsToInvalidate.Remove(name))
                    {
                        AddInvalidSong(name);
                    }
                }

                // Anything left in the dictionary may require invalidation of cached entries
                foreach (var leftover in songsToInvalidate)
                {
                    AddInvalidSong(leftover);
                }
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
            }
        }

        /// <summary>
        /// Loads all the upgrade nodes present in the cache from an "upgrade CON" section.
        /// </summary>
        /// <param name="stream">Stream containing the data for a CON's upgrade nodes</param>
        private void QuickReadUpgradeCON(FixedArrayStream stream)
        {
            var root = new AbridgedFileInfo(ref stream);
            var listings = GetCacheCONListings(root.FullName);
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                CONFileListing? listing = null;
                listings?.FindListing(PackedRBProUpgrade.UPGRADES_DIRECTORY + name + RBProUpgrade.UPGRADES_MIDI_EXT, out listing);
                var mods = GetQuickCONMods(name);
                lock (mods)
                {
                    if (mods.Upgrade == null || mods.Upgrade.LastWriteTime < root.LastWriteTime)
                    {
                        mods.Upgrade = new PackedRBProUpgrade(listing, in root);
                    }
                }
            }
        }

        private void ReadIniDirectory(FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = stream.ReadString();
            if (!GetBaseIniGroup(directory, out var baseGroup))
            {
                return;
            }

            unsafe
            {
                Parallel.ForEach(new CacheLoopable(&stream), node =>
                {
                    var entry = UnpackedIniEntry.TryDeserialize(directory, ref node.Slice, strings);
                    if (entry != null)
                    {
                        FindOrMarkDirectory(entry.ActualLocation);
                        AddEntry(entry);
                        baseGroup.AddEntry(entry);
                    }
                });

                Parallel.ForEach(new CacheLoopable(&stream), node =>
                {
                    var entry = SngEntry.TryDeserialize(directory, ref node.Slice, strings);
                    if (entry != null)
                    {
                        FindOrMarkFile(entry.ActualLocation);
                        AddEntry(entry);
                        baseGroup.AddEntry(entry);
                    }
                });
            }
        }

        private void QuickReadIniDirectory(FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = stream.ReadString();
            unsafe
            {
                Parallel.ForEach(new CacheLoopable(&stream), node => AddEntry(UnpackedIniEntry.ForceDeserialize(directory, ref node.Slice, strings)));
                Parallel.ForEach(new CacheLoopable(&stream), node => AddEntry(SngEntry.ForceDeserialize(directory, ref node.Slice, strings)));
            }
        }

        private void ReadCONGroup(FixedArrayStream stream, CacheReadStrings strings, bool fullDirectoryPlaylists)
        {
            string location = stream.ReadString();
            if (!GetBaseIniGroup(location, out var baseGroup))
            {
                return;
            }

            var lastWriteTime = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            string defaultPlaylist = ConstructPlaylist(location, baseGroup.Directory, fullDirectoryPlaylists);

            CONEntryGroup? group = null;
            if (stream.ReadBoolean())
            {
                lock (conEntryGroups)
                {
                    group = conEntryGroups.Find(node => node.Root.FullName == location);
                }

                if (group == null)
                {
                    var info = new FileInfo(location);
                    if (info.Exists)
                    {
                        FindOrMarkFile(location);
                        group = CreateCONGroup(info, defaultPlaylist).Entries;
                    }
                }
            }
            else
            {
                var dtaInfo = new FileInfo(Path.Combine(location, CONEntryGroup.SONGS_DTA));
                if (dtaInfo.Exists)
                {
                    FindOrMarkDirectory(location);
                    if (UnpackedCONEntryGroup.Create(location, dtaInfo, defaultPlaylist, out var unpacked))
                    {
                        lock (conEntryGroups)
                        {
                            conEntryGroups.Add(unpacked);
                        }
                        group = unpacked;
                    }
                }
            }

            if (group == null || group.Root.LastWriteTime != lastWriteTime)
            {
                return;
            }

            unsafe
            {
                Parallel.ForEach(new CacheLoopable(&stream), node =>
                {
                    try
                    {
                        string name = node.Slice.ReadString();
                        if (invalidSongsInCache.Contains(name))
                        {
                            return;
                        }

                        int index = node.Slice.ReadByte();
                        group.DeserializeEntry(ref node.Slice, name, index, strings);
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e);
                    }
                });
            }
        }

        private void QuickReadCONGroup(FixedArrayStream stream, CacheReadStrings strings)
        {
            var root = new AbridgedFileInfo(ref stream);
            List<CONFileListing>? listings = null;
            bool packed = stream.ReadBoolean();
            if (packed)
            {
                listings = GetCacheCONListings(root.FullName);
            }

            unsafe
            {
                Parallel.ForEach(new CacheLoopable(&stream), node =>
                {
                    string name = node.Slice.ReadString();
                    int index = node.Slice.ReadByte();
                    RBCONEntry entry = packed
                            ? PackedRBCONEntry.ForceDeserialize(listings, in root, name, ref node.Slice, strings)
                            : UnpackedRBCONEntry.ForceDeserialize(in root, name, ref node.Slice, strings);

                    if (cacheCONModifications.TryGetValue(name, out var mods))
                    {
                        entry.UpdateInfo(mods.UpdateDirectoryAndDtaLastWrite, mods.UpdateMidi, mods.Upgrade);
                    }
                    AddEntry(entry);
                });
            }
        }

        /// <summary>
        /// Grabs the iniGroup that parents the provided path, if one exists
        /// </summary>
        /// <param name="path">The absolute file path</param>
        /// <returns>The applicable group if found; <see langword="null"/> otherwise</returns>
        private bool GetBaseIniGroup(string path, out IniEntryGroup baseGroup)
        {
            foreach (var group in iniGroups)
            {
                if (path.StartsWith(group.Directory) &&
                    // Ensures directories with similar names (previously separate bases)
                    // that are consolidated in-game to a single base directory
                    // don't have conflicting "relative path" issues
                    (path.Length == group.Directory.Length || path[group.Directory.Length] == Path.DirectorySeparatorChar))
                {
                    baseGroup = group;
                    return true;
                }
            }
            baseGroup = null!;
            return false;
        }

        private QuickCONMods GetQuickCONMods(string name)
        {
            QuickCONMods mods;
            lock (cacheCONModifications)
            {
                if (!cacheCONModifications.TryGetValue(name, out mods))
                {
                    cacheCONModifications.Add(name, mods = new QuickCONMods());
                }
            }
            return mods;
        }

        /// <summary>
        /// Marks a CON song with the DTA name as invalid for addition from the cache
        /// </summary>
        /// <param name="name">The DTA name to mark</param>
        private void AddInvalidSong(string name)
        {
            lock (invalidSongsInCache)
            {
                invalidSongsInCache.Add(name);
            }
        }

        private List<CONFileListing>? GetCacheCONListings(string filename)
        {
            List<CONFileListing>? listings = null;
            lock (cacheCONListings)
            {
                if (!cacheCONListings.TryGetValue(filename, out listings))
                {
                    if (File.Exists(filename))
                    {
                        using var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        listings = CONFile.TryParseListings(filename, filestream);
                    }
                    cacheCONListings.Add(filename, listings);
                }
            }
            return listings;
        }

        private struct PackedGroupResult
        {
            public PackedCONEntryGroup? Entries;
            public PackedCONUpgradeGroup? Upgrades;
        }

        /// <summary>
        /// Attempts to create a PackedCONGroup with the provided fileinfo.
        /// </summary>
        /// <param name="info">The file info for the possible CONFile</param>
        /// <param name="defaultPlaylist">The playlist to use for any entries generated from the CON (if it is one)</param>
        /// <returns>A PackedCONGroup instance on success; <see langword="null"/> otherwise</returns>
        private PackedGroupResult CreateCONGroup(FileInfo info, string defaultPlaylist)
        {
            var result = default(PackedGroupResult);
            try
            {
                using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                var listings = CONFile.TryParseListings(info.FullName, stream);
                if (listings != null)
                {
                    var abridged = new AbridgedFileInfo(info);
                    try
                    {
                        if (PackedCONEntryGroup.Create(stream, listings, in abridged, defaultPlaylist, out result.Entries))
                        {
                            lock (conEntryGroups)
                            {
                                conEntryGroups.Add(result.Entries!);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e, $"Error while loading {info.FullName} - for song entries");
                    }

                    try
                    {
                        if (PackedCONUpgradeGroup.Create(stream, listings, in abridged, out result.Upgrades))
                        {
                            lock (packedUpgradeGroups)
                            {
                                packedUpgradeGroups.Add(result.Upgrades!);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e, $"Error while loading {info.FullName} - for song upgrades");
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {info.FullName}");
            }
            return result;
        }

        /// <summary>
        /// Constructs a directory-based playlist based on the provided file name
        /// </summary>
        /// <param name="filename">The path for the current file</param>
        /// <param name="baseDirectory">One of the base directories provided by the user</param>
        /// <returns>The default playlist to potentially use</returns>
        private string ConstructPlaylist(string filename, string baseDirectory, bool fullDirectoryPlaylists)
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

    internal static class AudioFinder
    {
        public static bool ContainsAudio(this SngFile sngFile)
        {
            foreach (var file in sngFile.Listings)
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
