using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Audio;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Extensions;

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
            public DateTime LastUpdatedTime => chartFile.LastUpdatedTime;

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

                // Flag that says "this is NOT a sng file"
                writer.Write(false);
                writer.Write(relative);
                writer.Write((byte) chartType);
                writer.Write(chartFile.LastUpdatedTime.ToBinary());
                if (iniFile != null)
                {
                    writer.Write(true);
                    writer.Write(iniFile.LastUpdatedTime.ToBinary());
                }
                else
                    writer.Write(false);
            }

            public Stream? GetChartStream()
            {
                if (!chartFile.IsStillValid())
                    return null;

                if (iniFile != null)
                {
                    if (!iniFile.IsStillValid())
                        return null;
                }
                else if (File.Exists(Path.Combine(directory, "song.ini")))
                {
                    return null;
                }

                return new FileStream(chartFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            }

            public Dictionary<SongStem, Stream> GetAudioStreams(params SongStem[] ignoreStems)
            {
                Dictionary<string, string> files = new();
                {
                    var parsed = System.IO.Directory.GetFiles(directory);
                    foreach (var file in parsed)
                        files.Add(Path.GetFileName(file).ToLower(), file);
                }

                Dictionary<SongStem, Stream> streams = new();
                foreach (var stem in IniAudioChecker.SupportedStems)
                {
                    var stemEnum = AudioHelpers.SupportedStems[stem];
                    if (ignoreStems.Contains(stemEnum))
                        continue;

                    foreach (var format in IniAudioChecker.SupportedFormats)
                    {
                        var audioFile = stem + format;
                        if (files.TryGetValue(audioFile, out var fullname))
                        {
                            // No file buffer
                            streams.Add(stemEnum, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read, 1));
                            // Parse no duplicate stems
                            break;
                        }
                    }
                }
                return streams;
            }

            public byte[]? GetUnprocessedAlbumArt()
            {
                Dictionary<string, string> files = new();
                {
                    var parsed = System.IO.Directory.GetFiles(directory);
                    foreach (var file in parsed)
                        files.Add(Path.GetFileName(file).ToLower(), file);
                }

                foreach (string albumFile in IIniMetadata.ALBUMART_FILES)
                    if (files.TryGetValue(albumFile, out var fullname))
                        return File.ReadAllBytes(fullname);
                return null;
            }

            public (BackgroundType Type, Stream? Stream) GetBackgroundStream(BackgroundType selections)
            {
                Dictionary<string, string> files = new();
                {
                    var parsed = System.IO.Directory.GetFiles(directory);
                    foreach (var file in parsed)
                        files.Add(Path.GetFileName(file).ToLower(), file);
                }

                if ((selections & BackgroundType.Yarground) > 0)
                {
                    if (files.TryGetValue("bg.yarground", out var file))
                        return (BackgroundType.Yarground, new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                }

                if ((selections & BackgroundType.Video) > 0)
                {
                    foreach (var stem in IIniMetadata.BACKGROUND_FILENAMES)
                    {
                        foreach (var format in IIniMetadata.VIDEO_EXTENSIONS)
                        {
                            if (files.TryGetValue(stem + format, out var fullname))
                                return (BackgroundType.Video, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read));
                        }
                    }
                }

                if ((selections & BackgroundType.Image) > 0)
                {
                    foreach (var stem in IIniMetadata.BACKGROUND_FILENAMES)
                    {
                        foreach (var format in IIniMetadata.IMAGE_EXTENSIONS)
                        {
                            if (files.TryGetValue(stem + format, out var fullname))
                                return (BackgroundType.Image, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read));
                        }
                    }
                }
                return (default, null);
            }

            public Stream? GetPreviewAudioStream()
            {
                foreach (var format in IniAudioChecker.SupportedFormats)
                {
                    var audioFile = Path.Combine(directory, "preview" + format);
                    if (File.Exists(audioFile))
                        return new FileStream(audioFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                }
                return null;
            }
        }

        public static (ScanResult, SongMetadata?) FromIni(string chartDirectory, IniChartNode<FileInfo> chart, FileInfo? iniFile, string defaultPlaylist)
        {
            IniSection iniModifiers;
            AbridgedFileInfo? iniFileInfo = null;
            if (iniFile != null)
            {
                if ((iniFile.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) > 0)
                {
                    return (ScanResult.IniNotDownloaded, null);
                }

                iniModifiers = SongIniHandler.ReadSongIniFile(iniFile.FullName);
                iniFileInfo = new AbridgedFileInfo(iniFile);
            }
            else 
            {
                iniModifiers = new();
            }

            if ((chart.File.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) > 0)
            {
                return (ScanResult.ChartNotDownloaded, null);
            }

            byte[] file = File.ReadAllBytes(chart.File.FullName);
            var result = ScanIniChartFile(file, chart.Type, iniModifiers);
            if (result.Item2 == null)
            {
                return (result.Item1, null);
            }

            var abridged = new AbridgedFileInfo(chart.File);
            var unpacked = new UnpackedIniSubmetadata(chartDirectory, chart.Type, abridged, iniFileInfo);
            var metadata = new SongMetadata(unpacked, result.Item2, HashWrapper.Hash(file), iniModifiers, defaultPlaylist);
            return (result.Item1, metadata);
        }

        public static SongMetadata? IniFromCache(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var chartInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, chart.File), reader);
            if (chartInfo == null)
            {
                return null;
            }

            string iniFile = Path.Combine(directory, "song.ini");
            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                iniInfo = AbridgedFileInfo.TryParseInfo(iniFile, reader);
                if (iniInfo == null)
                {
                    return null;
                }
            }
            else if (File.Exists(iniFile))
            {
                return null;
            }

            UnpackedIniSubmetadata iniData = new(directory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        public static SongMetadata? IniFromCache_Quick(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var lastUpdated = DateTime.FromBinary(reader.Read<long>(Endianness.Little));

            var chartInfo = new AbridgedFileInfo(Path.Combine(directory, chart.File), lastUpdated);
            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                lastUpdated = DateTime.FromBinary(reader.Read<long>(Endianness.Little));
                iniInfo = new AbridgedFileInfo(Path.Combine(directory, "song.ini"), lastUpdated);
            }

            UnpackedIniSubmetadata iniData = new(directory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }
    }
}
