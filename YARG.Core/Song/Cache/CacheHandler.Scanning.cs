using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public abstract partial class CacheHandler
    {
        protected sealed class FileCollector
        {
            public readonly DirectoryInfo directory;
            public readonly IniChartNode<FileInfo>?[] charts = new IniChartNode<FileInfo>?[3];
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
                                case "notes.mid": charts[0] = new (ChartType.Mid, subFile); break;
                                case "notes.midi": charts[1] = new (ChartType.Midi, subFile); break;
                                case "notes.chart": charts[2] = new (ChartType.Chart, subFile); break;
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
                return subfiles.Any(subFile => IniAudio.IsAudioFile(subFile.Name.ToLower()));
            }
        }

        protected struct PlaylistTracker
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

        private sealed class SngCollector
        {
            public readonly SngFile sng;
            public readonly IniChartNode<string>?[] charts = new IniChartNode<string>?[3];

            public SngCollector(SngFile sng)
            {
                this.sng = sng;
                for (int i = 0; i < IniSubEntry.CHART_FILE_TYPES.Length; ++i)
                {
                    if (sng.ContainsKey(IniSubEntry.CHART_FILE_TYPES[i].File))
                    {
                        charts[i] = IniSubEntry.CHART_FILE_TYPES[i];
                    }
                }
            }

            public bool ContainsAudio()
            {
                return sng.Any(subFile => IniAudio.IsAudioFile(subFile.Key));
            }
        }

        protected abstract void FindNewEntries();
        protected abstract void TraverseDirectory(FileCollector collector, IniGroup group, PlaylistTracker tracker);
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
                if (!TraversalPreTest(directory, tracker.Playlist))
                    return;

                var collector = new FileCollector(directory);
                if (ScanIniEntry(collector, group, tracker.Playlist))
                {
                    if (collector.subDirectories.Count > 0)
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                    return;
                }

                tracker.Append(directory.FullName);
                TraverseDirectory(collector, group, tracker);
            }
            catch (PathTooLongException)
            {
                YargTrace.LogError($"Path {directory.FullName} is too long for the file system!");
                AddToBadSongs(directory.FullName, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, $"Error while scanning directory {directory.FullName}!");
            }
        }

        protected void ScanFile(FileInfo info, IniGroup group, ref PlaylistTracker tracker)
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

        protected void TraverseCONGroup(YARGDTAReader reader, Action<string, int> func)
        {
            Dictionary<string, int> indices = new();
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                if (indices.TryGetValue(name, out int index))
                {
                    ++index;
                }
                indices[name] = index;

                func(name, index);
                reader.EndNode();
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
                    CreateUpdateGroup(dirInfo, abridged, true);
                    return false;
                }
            }
            else if (filename == "songs_upgrades")
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
                    AddUnpackedCONGroup(new UnpackedCONGroup(directory, dta, defaultPlaylist));
                    return false;
                }
            }
            return true;
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
                    var entry = UnpackedIniEntry.ProcessNewEntry(collector.directory.FullName, chart, collector.ini, defaultPlaylist);
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
                    var entry = SngEntry.ProcessNewEntry(sngFile, chart, defaultPlaylist);
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
            TryParseUpgrades(info.FullName, group);
            AddPackedCONGroup(group);
            return true;
        }
    }
}
