using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private sealed class FileCollector
        {
            public readonly DirectoryInfo directory;
            public readonly SongMetadata.IniChartNode<FileInfo>?[] charts = new SongMetadata.IniChartNode<FileInfo>?[3];
            public FileInfo? ini = null;
            public readonly List<FileInfo> subfiles = new();
            public readonly List<DirectoryInfo> subDirectories = new();

            public FileCollector(DirectoryInfo directory)
            {
                this.directory = directory;
                foreach (var info in directory.EnumerateFileSystemInfos())
                {
                    switch (info)
                    {
                        case FileInfo subFile:
                        {
                            switch (subFile.Name.ToLower())
                            {
                                case "song.ini": ini = subFile; break;
                                case "notes.mid": charts[0] = new (SongMetadata.ChartType.Mid, subFile); break;
                                case "notes.midi": charts[1] = new (SongMetadata.ChartType.Midi, subFile); break;
                                case "notes.chart": charts[2] = new (SongMetadata.ChartType.Chart, subFile); break;
                                default: subfiles.Add(subFile); break;
                            }
                            break;
                        }
                        case DirectoryInfo subDirectory:
                        {
                            subDirectories.Add(subDirectory);
                            break;
                        }
                    }
                }
            }

            public bool ContainsAudio()
            {
                foreach (var subFile in subfiles)
                {
                    if (SongMetadata.IniAudioChecker.IsAudioFile(subFile.Name.ToLower()))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private sealed class SngCollector
        {
            public readonly SngFile sng;
            public readonly SongMetadata.IniChartNode<string>?[] charts = new SongMetadata.IniChartNode<string>?[3];

            public SngCollector(SngFile sng)
            {
                this.sng = sng;
                for (int i = 0; i < SongMetadata.IIniMetadata.CHART_FILE_TYPES.Length; ++i)
                {
                    if (sng.ContainsKey(SongMetadata.IIniMetadata.CHART_FILE_TYPES[i].File))
                    {
                        charts[i] = SongMetadata.IIniMetadata.CHART_FILE_TYPES[i];
                    }
                }
            }

            public bool ContainsAudio()
            {
                foreach (var subFile in sng)
                {
                    if (SongMetadata.IniAudioChecker.IsAudioFile(subFile.Key))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private struct PlaylistTracker
        {
            private readonly bool _fullDirectoryFlag;
            private string? _tracker;
            private string _playlist;

            public readonly string Playlist => _playlist;

            public PlaylistTracker(bool fullDirectoryFlag)
            {
                _fullDirectoryFlag = fullDirectoryFlag;
                _tracker = null;
                _playlist = "Unknown Playlist";
            }

            public void Append(string directory)
            {
                if (_tracker != null)
                {
                    string subDir = Path.GetFileName(directory);
                    if (_fullDirectoryFlag)
                    {
                        _playlist = _tracker = Path.Combine(_tracker, subDir);
                    }
                    else
                    {
                        _playlist = subDir;
                    }
                }
                else
                {
                    _tracker = string.Empty;
                }
            }
        }

        private void FindNewEntries(bool multithreading)
        {
            _progress.Stage = ScanStage.LoadingSongs;
            var tracker = new PlaylistTracker(fullDirectoryPlaylists);
            if (multithreading)
            {
                if (iniGroups.Count > 1)
                {
                    var iniTasks = new Task[iniGroups.Count];
                    for (int i = 0; i < iniGroups.Count; ++i)
                    {
                        var group = iniGroups[i];
                        var dirInfo = new DirectoryInfo(group.Directory);
                        iniTasks[i] = Task.Run(() => ScanDirectory_Parallel(dirInfo, group, tracker));
                    }
                    Task.WaitAll(iniTasks);
                }
                else if (iniGroups.Count == 1)
                {
                    var group = iniGroups[0];
                    var dirInfo = new DirectoryInfo(group.Directory);
                    ScanDirectory_Parallel(dirInfo, group, tracker);
                }

                // Orders the updates from oldest to newest to apply more recent information last
                Parallel.ForEach(updates, node => node.Value.Sort());

                var conTasks = new Task[conGroups.Values.Count + extractedConGroups.Values.Count];
                int con = 0;
                foreach (var group in conGroups.Values)
                {
                    conTasks[con++] = Task.Run(() => ScanCONGroup_Parallel(group));
                }

                foreach (var group in extractedConGroups.Values)
                {
                    conTasks[con++] = Task.Run(() => ScanExtractedCONGroup_Parallel(group));
                }

                Task.WaitAll(conTasks);
            }
            else
            {
                foreach (var group in iniGroups)
                {
                    var dirInfo = new DirectoryInfo(group.Directory);
                    ScanDirectory(dirInfo, group, tracker);
                }

                foreach (var (_, list) in updates)
                {
                    // Orders the updates from oldest to newest to apply more recent information last
                    list.Sort();
                }

                foreach (var group in conGroups.Values)
                {
                    ScanCONGroup(group);
                }

                foreach (var group in extractedConGroups.Values)
                {
                    ScanExtractedCONGroup(group);
                }
            }
        }

        private bool TraversalPreTest(DirectoryInfo dirInfo, string defaultPlaylist)
        {
            string directory = dirInfo.FullName;
            if (!FindOrMarkDirectory(dirInfo.FullName) || (dirInfo.Attributes & FileAttributes.Hidden) != 0)
                return false;

            string filename = dirInfo.Name;
            if (filename == "songs_updates")
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    var abridged = new AbridgedFileInfo(dta, false);
                    CreateUpdateGroup(directory, abridged, true);
                    return false;
                }
            }
            else if (filename == "song_upgrades")
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    var abridged = new AbridgedFileInfo(dta, false);
                    CreateUpgradeGroup(directory, abridged, true);
                    return false;
                }
            }
            else if (filename == "songs")
            {
                FileInfo dta = new(Path.Combine(directory, "songs.dta"));
                if (dta.Exists)
                {
                    extractedConGroups.Add(new UnpackedCONGroup(directory, dta, defaultPlaylist));
                    return false;
                }
            }
            return true;
        }
        
        private void ScanFile(FileInfo info, IniGroup group, ref PlaylistTracker tracker)
        {
            string filename = info.FullName;
            try
            {
                // Ensures only fully downloaded unmarked files are processed
                if (FindOrMarkFile(filename) && (info.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) == 0)
                {
                    var abridged = new AbridgedFileInfo(info);
                    if (!AddPossibleCON(abridged, tracker.Playlist) && (filename.EndsWith(".sng") || filename.EndsWith(".yargsong")))
                    {
                        ScanSngFile(abridged, group, tracker.Playlist);
                    }
                }
            }
            catch (PathTooLongException)
            {
                YargTrace.LogError($"Path {filename} is too long for the file system!");
                AddToBadSongs(filename, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning file {filename}!");
            }
        }

        private bool ScanIniEntry(FileCollector collector, IniGroup group, string defaultPlaylist)
        {
            for (int i = collector.ini != null ? 0 : 2; i < 3; ++i)
            {
                var chart = collector.charts[i];
                if (chart == null)
                {
                    continue;
                }

                if (!collector.ContainsAudio())
                {
                    AddToBadSongs(chart.File.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = SongMetadata.FromIni(collector.directory.FullName, chart, collector.ini, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(chart.File.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (PathTooLongException)
                {
                    YargTrace.LogError($"Path {chart.File} is too long for the file system!");
                    AddToBadSongs(chart.File.FullName, ScanResult.PathTooLong);
                }
                catch (Exception e)
                {
                    YargTrace.LogException(e, $"Error while scanning chart file {chart.File}!");
                    AddToBadSongs(collector.directory.FullName, ScanResult.IniEntryCorruption);
                }
                return true;
            }
            return false;
        }

        private bool ScanSngFile(AbridgedFileInfo info, IniGroup group, string defaultPlaylist)
        {
            var sngFile = SngFile.TryLoadFromFile(info);
            if (sngFile == null)
            {
                AddToBadSongs(info.FullName, ScanResult.PossibleCorruption);
                return false;
            }

            var collector = new SngCollector(sngFile);
            for (int i = sngFile.Metadata.Count > 0 ? 0 : 2; i < 3; ++i)
            {
                var chart = collector.charts[i];
                if (chart == null)
                {
                    continue;
                }

                if (!collector.ContainsAudio())
                {
                    AddToBadSongs(info.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = SongMetadata.FromSng(sngFile, chart, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(info.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (Exception e)
                {
                    YargTrace.LogException(e, $"Error while scanning chart file {chart} within {info}!");
                    AddToBadSongs(info.FullName, ScanResult.IniEntryCorruption);
                }
                break;
            }
            return true;
        }

        private bool AddPossibleCON(AbridgedFileInfo info, string defaultPlaylist)
        {
            var file = CONFile.TryLoadFile(info);
            if (file == null)
                return false;

            var group = new PackedCONGroup(file, info, defaultPlaylist);
            conGroups.Add(group);
            TryParseUpgrades(info.FullName, group);
            return true;
        }

        private int GetCONIndex(Dictionary<string, int> indices, string name)
        {
            if (indices.ContainsKey(name))
                return ++indices[name];
            return indices[name] = 0;
        }

        private void ScanPackedCONNode(PackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                    group.RemoveEntry(name, index);
            }
            else
            {
                var song = SongMetadata.FromPackedRBCON(group, name, node, updates, upgrades);
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

        private void ScanUnpackedCONNode(UnpackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                    group.RemoveEntry(name, index);
            }
            else
            {
                var song = SongMetadata.FromUnpackedRBCON(group, name, node, updates, upgrades);
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

        private bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (preScannedDirectories.Contains(directory))
                    return false;

                preScannedDirectories.Add(directory);
                _progress.NumScannedDirectories++;
                return true;
            }
        }

        private bool FindOrMarkFile(string file)
        {
            lock (fileLock)
            {
                if (preScannedFiles.Contains(file))
                    return false;

                preScannedFiles.Add(file);
                return true;
            }
        }

        private void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badsongsLock)
            {
                badSongs.Add(filePath, err);
                _progress.BadSongCount++;
            }
        }
    }
}
