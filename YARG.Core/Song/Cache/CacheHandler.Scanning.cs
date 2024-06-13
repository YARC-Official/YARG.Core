using System;
using System.IO;
using System.Linq;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public abstract partial class CacheHandler
    {
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

        protected abstract void FindNewEntries();
        protected abstract void TraverseDirectory(in FileCollection collection, IniGroup group, PlaylistTracker tracker);
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

                if (ScanIniEntry(collection, group, tracker.Playlist))
                {
                    if (collection.subDirectories.Count > 0)
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                    return;
                }

                switch (directory.Name)
                {
                    case "songs_updates":
                    {
                        if (collection.subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
                        {
                            var updateGroup = CreateUpdateGroup(in collection, dta, true);
                            if (updateGroup != null)
                            {
                                AddUpdateGroup(updateGroup);
                                return;
                            }
                        }
                        break;
                    }
                    case "songs_upgrades":
                    {
                        if (collection.subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
                        {
                            var upgradeGroup = CreateUpgradeGroup(in collection, dta, true);
                            if (upgradeGroup != null)
                            {
                                AddUpgradeGroup(upgradeGroup);
                                return;
                            }
                        }
                        break;
                    }
                    case "songs":
                    {
                        if (collection.subfiles.TryGetValue(SONGS_DTA, out var dta))
                        {
                            var exConGroup = new UnpackedCONGroup(directory.FullName, dta, tracker.Playlist);
                            AddUnpackedCONGroup(exConGroup);
                            return;
                        }
                        break;
                    }
                }
                TraverseDirectory(collection, group, tracker.Append(directory.Name));
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
                    if (ext.Length == 0)
                    {
                        var confile = CONFile.TryParseListings(abridged);
                        if (confile != null)
                        {
                            var conGroup = new PackedCONGroup(confile.Value, abridged, tracker.Playlist);
                            TryParseUpgrades(info.FullName, conGroup);
                            AddPackedCONGroup(conGroup);
                        }
                    }
                    else if (ext == ".sng" || ext == ".yargsong")
                    {
                        var sngFile = SngFile.TryLoadFromFile(abridged);
                        if (sngFile != null)
                        {
                            ScanSngFile(sngFile, group, tracker.Playlist);
                        }
                        else
                        {
                            AddToBadSongs(info.FullName, ScanResult.PossibleCorruption);
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

        protected void ScanPackedCONNode(PackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                    group.RemoveEntry(name, index);
            }
            else
            {
                var song = PackedRBCONEntry.ProcessNewEntry(group, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(group.Location + $" - Node {name}", song.Item1);
                }
            }
        }

        protected void ScanUnpackedCONNode(UnpackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                    group.RemoveEntry(name, index);
            }
            else
            {
                var song = UnpackedRBCONEntry.ProcessNewEntry(group, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(group.Location + $" - Node {name}", song.Item1);
                }
            }
        }

        private static readonly (string Name, ChartType Type)[] ChartTypes =
        {
            ("notes.mid", ChartType.Mid),
            ("notes.midi", ChartType.Midi),
            ("notes.chart", ChartType.Chart)
        };

        private bool ScanIniEntry(in FileCollection collection, IniGroup group, string defaultPlaylist)
        {
            int i = collection.subfiles.TryGetValue("song.ini", out var ini) ? 0 : 2;
            while (i < 3)
            {
                if (!collection.subfiles.TryGetValue(ChartTypes[i].Name, out var chart))
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
                    var node = new IniChartNode<FileInfo>(chart, ChartTypes[i].Type);
                    var entry = UnpackedIniEntry.ProcessNewEntry(collection.Directory.FullName, in node, ini, defaultPlaylist);
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
                if (!sngFile.TryGetValue(ChartTypes[i].Name, out var chart))
                {
                    ++i;
                    continue;
                }

                if (!sngFile.Any(subFile => IniAudio.IsAudioFile(subFile.Key)))
                {
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var node = new IniChartNode<SngFileListing>(chart, ChartTypes[i].Type);
                    var entry = SngEntry.ProcessNewEntry(sngFile, in node, defaultPlaylist);
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
                    YargLogger.LogException(e, $"Error while scanning chart file {chart} within {sngFile.Info}!");
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.IniEntryCorruption);
                }
                break;
            }
        }
    }
}
