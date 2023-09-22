using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song.Cache
{
    public sealed partial class CacheHandler
    {
        private class FileCollector
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
            Progress = ScanProgress.LoadingSongs;
            if (multithreading)
            {
                Parallel.For(0, baseDirectories.Length, i => ScanDirectory_Parallel(baseDirectories[i], i));

                Task.WaitAll(Task.Run(() => Parallel.ForEach(conGroups, ScanCONGroup_Parallel)),
                             Task.Run(() => Parallel.ForEach(extractedConGroups, ScanExtractedCONGroup_Parallel)));
            }
            else
            {
                for (int i = 0; i < baseDirectories.Length; ++i)
                    ScanDirectory(baseDirectories[i], i);

                foreach (var node in conGroups)
                    ScanCONGroup(node);

                foreach (var node in extractedConGroups)
                    ScanExtractedCONGroup(node);
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
                    AddExtractedCONGroup(new(directory, dta));
                    return false;
                }
            }
            return true;
        }

        private bool ScanIniEntry(FileCollector results, int index)
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
                                AddIniEntry(entry.Item2, index);
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

            var file = CONFile.LoadCON(filename);
            if (file == null)
                return;

            PackedCONGroup group = new(file, File.GetLastWriteTime(filename));
            AddCONGroup(group);

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

        private void ScanPackedCONNode(PackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!) && group.RemoveEntry(name, index))
                    YargTrace.DebugInfo($"{group.file.filename} - {name} removed as duplicate");
            }
            else
            {
                var song = SongMetadata.FromPackedRBCON(group.file, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(group.file.filename + $" - Node {name}", song.Item1);
                }
            }
        }

        private void ScanUnpackedCONNode(UnpackedCONGroup group, string name, int index, YARGDTAReader node)
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!) && group.RemoveEntry(name, index))
                    YargTrace.DebugInfo($"{group.directory} - {name} removed as duplicate");
            }
            else
            {
                var song = SongMetadata.FromUnpackedRBCON(group.directory, group.dta, name, node, updates, upgrades);
                if (song.Item2 != null)
                {
                    if (AddEntry(song.Item2))
                        group.AddEntry(name, index, song.Item2);
                }
                else
                {
                    AddToBadSongs(group.directory + $" - Node {name}", song.Item1);
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
