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
            public readonly string?[] charts = new string?[3];
            public string? ini = null;
            public readonly List<string> subfiles = new();

            private FileCollector(string directory)
            {
                this.directory = directory;
            }

            public static FileCollector Collect(string directory)
            {
                FileCollector files = new(directory);
                foreach (string subFile in Directory.EnumerateFileSystemEntries(directory))
                {
                    switch (Path.GetFileName(subFile).ToLower())
                    {
                        case "song.ini": files.ini = subFile; break;
                        case "notes.mid": files.charts[0] = subFile; break;
                        case "notes.midi": files.charts[1] = subFile; break;
                        case "notes.chart": files.charts[2] = subFile; break;
                        default: files.subfiles.Add(subFile); break;
                    }
                }
                return files;
            }
        }

        private void FindNewEntries()
        {
            static void ParallelLoop<T>(Dictionary<string, T> groups, Action<string, T> action)
            {
                Parallel.ForEach(groups, group => action(group.Key, group.Value));
            }

            static void SequentialLoop<T>(Dictionary<string, T> groups, Action<string, T> action)
            {
                foreach (var group in groups)
                    action(group.Key, group.Value);
            }

            Progress = ScanProgress.LoadingSongs;
            if (multithreading)
            {
                ParallelLoop(iniGroups, ScanDirectory_Parallel);
                Task.WaitAll(Task.Run(() => ParallelLoop(conGroups, ScanCONGroup_Parallel)),
                             Task.Run(() => ParallelLoop(extractedConGroups, ScanExtractedCONGroup_Parallel)));
            }
            else
            {
                SequentialLoop(iniGroups, ScanDirectory);
                SequentialLoop(conGroups, ScanCONGroup);
                SequentialLoop(extractedConGroups, ScanExtractedCONGroup);
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
                    AddExtractedCONGroup(directory, new(dta));
                    return false;
                }
            }
            return true;
        }

        private bool ScanIniEntry(FileCollector results, IniGroup group)
        {
            for (int i = results.ini != null ? 0 : 2; i < 3; ++i)
            {
                string? chart = results.charts[i];
                if (chart != null)
                {
                    try
                    {
                        byte[] file = File.ReadAllBytes(chart);
                        var entry = SongMetadata.FromIni(file, chart, results.ini, i);
                        if (entry.Item2 != null)
                        {
                            if (AddEntry(entry.Item2))
                                group.AddEntry(entry.Item2);
                        }
                        else if (entry.Item1 != ScanResult.LooseChart_NoAudio)
                            AddToBadSongs(chart, entry.Item1);
                        else
                            return false;
                    }
                    catch (PathTooLongException)
                    {
                        YargTrace.LogWarning($"Path {chart} is too long for the file system!");
                        AddToBadSongs(chart, ScanResult.PathTooLong);
                    }
                    catch (Exception e)
                    {
                        YargTrace.LogException(e, $"Error while scanning chart file {chart}!");
                        AddToBadSongs(Path.GetDirectoryName(chart), ScanResult.IniEntryCorruption);
                    }
                    return true;
                }
            }
            return false;
        }

        private void AddPossibleCON(string filename)
        {
            if (!FindOrMarkFile(filename))
                return;

            var files = CONFileHandler.TryParseListings(filename);
            if (files == null)
                return;

            PackedCONGroup group = new(files, File.GetLastWriteTime(filename));
            AddCONGroup(filename, group);

            var reader = group.LoadUpgrades();
            if (reader != null)
                AddCONUpgrades(group, reader);
        }

        private int GetCONIndex(Dictionary<string, int> indices, string name)
        {
            if (indices.ContainsKey(name))
                return ++indices[name];
            return indices[name] = 0;
        }

        private void ScanPackedCONNode(string filename, PackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!) && group.RemoveEntry(name, index))
                    YargTrace.DebugInfo($"{filename} - {name} removed as duplicate");
            }
            else
            {
                var song = SongMetadata.FromPackedRBCON(group.Files, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(filename + $" - Node {name}", song.Item1);
                }
            }
        }

        private void ScanUnpackedCONNode(string directory, UnpackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!) && group.RemoveEntry(name, index))
                    YargTrace.DebugInfo($"{directory} - {name} removed as duplicate");
            }
            else
            {
                var song = SongMetadata.FromUnpackedRBCON(directory, group.dta, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(directory + $" - Node {name}", song.Item1);
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
            lock (badsongsLock) badSongs.Add(filePath, err);
        }
    }
}
