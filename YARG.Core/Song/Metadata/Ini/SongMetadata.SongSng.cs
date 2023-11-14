using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public partial class SongMetadata
    {
        public sealed class SngSubmetadata : IIniMetadata
        {
            private readonly uint version;
            private readonly SngFile sngFile;
            private readonly AbridgedFileInfo sngInfo;
            private readonly IniChartNode chart;

            public string Root => sngInfo.FullName;
            public ChartType Type => chart.Type;

            public SngSubmetadata(uint version, SngFile sngFile, AbridgedFileInfo sngInfo, IniChartNode chart)
            {
                this.version = version;
                this.sngFile = sngFile;
                this.sngInfo = sngInfo;
                this.chart = chart;
            }

            public SngSubmetadata(SngFile sngFile, AbridgedFileInfo sngInfo, IniChartNode chart)
                : this(sngFile.Version ,sngFile, sngInfo, chart) { }

            public void Serialize(BinaryWriter writer, string groupDirectory)
            {
                string relative = Path.GetRelativePath(groupDirectory, sngInfo.FullName);
                if (relative == ".")
                    relative = string.Empty;

                writer.Write(true);
                writer.Write(version);
                writer.Write(relative);
                writer.Write(sngInfo.LastWriteTime.ToBinary());
                writer.Write((byte) chart.Type);
            }

            public Stream? GetChartStream()
            {
                // Possible place where versioning could be useful, but who knows
                return sngInfo.IsStillValid() ? sngFile[chart.File].CreateStream(sngInfo.FullName, sngFile.Mask) : null;
            }

            public List<Stream> GetAudioStreams()
            {
                List<Stream> streams = new();
                foreach (var stem in IniAudioChecker.SupportedStems)
                {
                    foreach (var format in IniAudioChecker.SupportedFormats)
                    {
                        var file = stem + format;
                        if (sngFile.ContainsKey(file))
                        {
                            streams.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1));
                            // Parse no duplicate stems
                            break;
                        }
                    }
                }
                return streams;
            }

            public static bool DoesSoloChartHaveAudio(SngFile sng)
            {
                foreach (var listing in sng)
                    if (IniAudioChecker.IsAudioFile(listing.Key))
                        return true;
                return false;
            }
        }

        public static (ScanResult, SongMetadata?) FromSng(SngFile sng, AbridgedFileInfo sngInfo, IniChartNode chart)
        {
            if (sng.Metadata.Count == 0 && !SngSubmetadata.DoesSoloChartHaveAudio(sng))
                return (ScanResult.LooseChart_NoAudio, null);

            SngSubmetadata metadata = new(sng, sngInfo, chart);
            byte[] file = sng[chart.File].LoadAllBytes(sngInfo.FullName, sng.Mask);
            var result = ScanIniChartFile(file, chart.Type, sng.Metadata);
            return (result.Item1, result.Item2 != null ? new(metadata, result.Item2, HashWrapper.Create(file), sng.Metadata) : null);
        }

        public static SongMetadata? SngFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            // Implement proper versioning in the future 
            uint version = reader.ReadUInt32();

            string sngPath = Path.Combine(baseDirectory, reader.ReadLEBString());
            var sngfile = SngFile.TryLoadFile(sngPath);
            // TODO: Implement Update-in-place functionality
            if (sngfile == null || sngfile.Version != version)
                return null;

            var sngInfo = AbridgedFileInfo.TryParseInfo(sngPath, reader);
            // Possibly could be handled differently in further versions of .sng
            // Example: allowing for per-subfile lastwrite comparsions
            if (sngInfo == null)
                return null;

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            SngSubmetadata sngData = new(sngfile, sngInfo, IIniMetadata.CHART_FILE_TYPES[chartTypeIndex]);
            return new SongMetadata(sngData, reader, strings)
            {
                _directory = sngPath
            };
        }

        public static SongMetadata? SngFromCache_Quick(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            uint version = reader.ReadUInt32();

            string sngPath = Path.Combine(baseDirectory, reader.ReadLEBString());
            var sngfile = SngFile.TryLoadFile(sngPath);
            if (sngfile == null)
            {
                YargTrace.DebugInfo($"Failed to load .sng from Cache file");
                return null;
            }

            AbridgedFileInfo sngInfo = new(sngPath, DateTime.FromBinary(reader.ReadInt64()));

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
            {
                YargTrace.DebugInfo($"Cache file was modified externally with a bad CHART_TYPE enum value");
                return null;
            }

            SngSubmetadata sngData = new(version, sngfile, sngInfo, IIniMetadata.CHART_FILE_TYPES[chartTypeIndex]);
            return new SongMetadata(sngData, reader, strings)
            {
                _directory = sngPath
            };
        }
    }
}
