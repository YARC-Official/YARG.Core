using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        [Serializable]
        public sealed class UnpackedIniSubmetadata : IIniMetadata
        {
            private readonly string directory;
            private readonly ChartType chartType;
            private readonly AbridgedFileInfo chartFile;
            private readonly AbridgedFileInfo? iniFile;

            public string Root => directory;
            public ChartType Type => chartType;

            public UnpackedIniSubmetadata(string directory, ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile)
            {
                this.directory = directory;
                this.chartType = chartType;
                this.chartFile = chartFile;
                this.iniFile = iniFile;
            }

            public void Serialize(BinaryWriter writer, string groupDirectory)
            {
                string relative = Path.GetRelativePath(groupDirectory, directory);
                if (relative == ".")
                    relative = string.Empty;

                writer.Write(false);
                writer.Write(relative);
                writer.Write((byte) chartType);
                writer.Write(chartFile.LastWriteTime.ToBinary());
                if (iniFile != null)
                {
                    writer.Write(true);
                    writer.Write(iniFile.LastWriteTime.ToBinary());
                }
                else
                    writer.Write(false);
            }

            public Stream? GetChartStream()
            {
                if (!chartFile.IsStillValid())
                    return null;

                if (iniFile == null)
                {
                    if (File.Exists(Path.Combine(directory, "song.ini")))
                        return null;
                }
                else if (!iniFile.IsStillValid())
                    return null;

                return new FileStream(chartFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            }

            public List<Stream> GetAudioStreams()
            {
                List<Stream> streams = new();
                HashSet<string> files = new();
                {
                    var parsed = System.IO.Directory.GetFiles(directory);
                    foreach (var file in parsed)
                        files.Add(Path.GetFileName(file));
                }

                foreach (var stem in IniAudioChecker.SupportedStems)
                {
                    foreach (var format in IniAudioChecker.SupportedFormats)
                    {
                        var file = stem + format;
                        if (files.Contains(file))
                        {
                            streams.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1));
                            // Parse no duplicate stems
                            break;
                        }
                    }
                }
                return streams;
            }

            

            public static bool DoesSoloChartHaveAudio(string directory)
            {
                foreach (string subFile in System.IO.Directory.EnumerateFileSystemEntries(directory))
                    if (IniAudioChecker.IsAudioFile(Path.GetFileName(subFile)))
                        return true;
                return false;
            }
        }

        public static (ScanResult, SongMetadata?) FromIni(string directory, IniChartNode chart, string? iniFile)
        {
            IniSection iniModifiers;
            AbridgedFileInfo? iniFileInfo = null;
            if (iniFile != null)
            {
                iniModifiers = SongIniHandler.ReadSongIniFile(iniFile);
                iniFileInfo = new AbridgedFileInfo(iniFile);
            }
            else if (UnpackedIniSubmetadata.DoesSoloChartHaveAudio(Path.GetDirectoryName(chart.File)))
                iniModifiers = new();
            else
                return (ScanResult.LooseChart_NoAudio, null);

            UnpackedIniSubmetadata metadata = new(directory, chart.Type, new AbridgedFileInfo(chart.File), iniFileInfo);

            byte[] file = File.ReadAllBytes(chart.File);
            var result = ScanIniChartFile(file, chart.Type, iniModifiers);
            return (result.Item1, result.Item2 != null ? new(metadata, result.Item2, HashWrapper.Create(file), iniModifiers) : null);
        }

        public static SongMetadata? IniFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var chartInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, chart.File), reader);
            if (chartInfo == null)
                return null;

            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                iniInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "song.ini"), reader);
                if (iniInfo == null)
                    return null;
            }
            else if (!UnpackedIniSubmetadata.DoesSoloChartHaveAudio(directory))
                return null;

            UnpackedIniSubmetadata iniData = new(baseDirectory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        public static SongMetadata? IniFromCache_Quick(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo chartInfo = new(Path.Combine(directory, chart.File), lastWrite);
            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                lastWrite = DateTime.FromBinary(reader.ReadInt64());
                iniInfo = new(Path.Combine(directory, "song.ini"), lastWrite);
            }

            UnpackedIniSubmetadata iniData = new(baseDirectory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }
    }
}
