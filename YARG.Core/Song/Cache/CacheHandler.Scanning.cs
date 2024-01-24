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
            public readonly string directory;
            public readonly SongMetadata.IniChartNode?[] charts = new SongMetadata.IniChartNode?[3];
            public string? ini = null;
            public readonly List<string> subfiles = new();

            public FileCollector(string directory)
            {
                this.directory = directory;
                foreach (string subFile in Directory.EnumerateFileSystemEntries(directory))
                {
                    switch (Path.GetFileName(subFile).ToLower())
                    {
                        case "song.ini": ini = subFile; break;
                        case "notes.mid": charts[0] = new(SongMetadata.ChartType.Mid, subFile); break;
                        case "notes.midi": charts[1] = new(SongMetadata.ChartType.Midi, subFile); break;
                        case "notes.chart": charts[2] = new(SongMetadata.ChartType.Chart, subFile); break;
                        default: subfiles.Add(subFile); break;
                    }
                }
            }
        }

        private sealed class SngCollector
        {
            public readonly SngFile sng;
            public readonly SongMetadata.IniChartNode?[] charts = new SongMetadata.IniChartNode?[3];

            public SngCollector(SngFile sng)
            {
                this.sng = sng;
                for (int i = 0; i < SongMetadata.IIniMetadata.CHART_FILE_TYPES.Length; ++i)
                    if (sng.ContainsKey(SongMetadata.IIniMetadata.CHART_FILE_TYPES[i].File))
                        charts[i] = SongMetadata.IIniMetadata.CHART_FILE_TYPES[i];
            }
        }

        private void FindNewEntries(bool multithreading)
        {
            _progress.Stage = ScanStage.LoadingSongs;
            if (multithreading)
            {
                Parallel.ForEach(iniGroups, group => ScanDirectory_Parallel(group.Directory, group));

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
                    ScanDirectory(group.Directory, group);
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

        private bool TraversalPreTest(string directory)
        {
            if (!FindOrMarkDirectory(directory) || (File.GetAttributes(directory) & FileAttributes.Hidden) != 0)
                return false;

            string filename = Path.GetFileName(directory);
            if (filename == "songs_updates")
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    CreateUpdateGroup(directory, dta, true);
                    return false;
                }
            }
            else if (filename == "song_upgrades")
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    CreateUpgradeGroup(directory, dta, true);
                    return false;
                }
            }
            else if (filename == "songs")
            {
                FileInfo dta = new(Path.Combine(directory, "songs.dta"));
                if (dta.Exists)
                {
                    extractedConGroups.Add(new UnpackedCONGroup(directory, dta));
                    return false;
                }
            }
            return true;
        }

        private bool ScanIniEntry(FileCollector results, IniGroup group)
        {
            for (int i = results.ini != null ? 0 : 2; i < 3; ++i)
            {
                var chart = results.charts[i];
                if (chart != null)
                {
                    try
                    {
                        var entry = SongMetadata.FromIni(results.directory, chart, results.ini);
                        if (entry.Item2 != null)
                        {
                            if (AddEntry(entry.Item2))
                                group.AddEntry(entry.Item2);
                        }
                        else if (entry.Item1 != ScanResult.LooseChart_NoAudio)
                            AddToBadSongs(chart.File, entry.Item1);
                        else
                            return false;
                    }
                    catch (PathTooLongException)
                    {
                        YargTrace.LogWarning($"Path {chart.File} is too long for the file system!");
                        AddToBadSongs(chart.File, ScanResult.PathTooLong);
                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error while scanning chart file {chart.File}!");
                        AddToBadSongs(Path.GetDirectoryName(chart.File), ScanResult.IniEntryCorruption);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool ScanSngFile(string filename, IniGroup group)
        {
            var sngFile = SngFile.TryLoadFromFile(filename);
            if (sngFile == null)
            {
                AddToBadSongs(filename, ScanResult.PossibleCorruption);
                return false;
            }

            var results = new SngCollector(sngFile);
            for (int i = sngFile.Metadata.Count != 0 ? 0 : 2; i < 3; ++i)
            {
                var chart = results.charts[i];
                if (chart == null)
                {
                    continue;
                }

                try
                {
                    var fileInfo = new AbridgedFileInfo(filename);
                    var entry = SongMetadata.FromSng(sngFile, fileInfo, chart);
                    if (entry.Item2 != null)
                    {
                        if (AddEntry(entry.Item2))
                        {
                            group.AddEntry(entry.Item2);
                        }
                    }
                    else if (entry.Item1 != ScanResult.LooseChart_NoAudio)
                    {
                        AddToBadSongs(filename, entry.Item1);
                    }
                }
                catch (Exception e)
                {
                    YargTrace.LogException(e, $"Error while scanning chart file {chart} within {filename}!");
                    AddToBadSongs(filename, ScanResult.IniEntryCorruption);
                }
                break;
            }
            return true;
        }

        private bool AddPossibleCON(string filename)
        {
            var result = CONFile.TryLoadFile(filename);
            if (result == null)
                return false;

            var group = new PackedCONGroup(result.File, result.Info);
            conGroups.Add(group);
            TryParseUpgrades(filename, group);
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
