using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public partial class SongMetadata
    {
        public sealed class SngSubmetadata : IIniMetadata
        {
            private readonly uint version;
            private readonly AbridgedFileInfo sngInfo;
            private readonly IniChartNode chart;

            public string Root => sngInfo.FullName;
            public ChartType Type => chart.Type;

            public SngSubmetadata(uint version, AbridgedFileInfo sngInfo, IniChartNode chart)
            {
                this.version = version;
                this.sngInfo = sngInfo;
                this.chart = chart;
            }

            public void Serialize(BinaryWriter writer, string groupDirectory)
            {
                string relative = Path.GetRelativePath(groupDirectory, sngInfo.FullName);
                if (relative == ".")
                    relative = string.Empty;

                // Flag that says "this IS a sng file"
                writer.Write(true);

                writer.Write(version);
                writer.Write(relative);
                writer.Write(sngInfo.LastWriteTime.ToBinary());
                writer.Write((byte) chart.Type);
            }

            public Stream? GetChartStream()
            {
                if (!sngInfo.IsStillValid())
                    return null;

                var sngFile = SngFile.TryLoadFromFile(sngInfo.FullName);
                if (sngFile == null)
                    return null;

                return sngFile.CreateStream(sngFile[chart.File]);
            }

            public Dictionary<SongStem, Stream> GetAudioStreams()
            {
                Dictionary<SongStem, Stream> streams = new();

                var sngFile = SngFile.TryLoadFromFile(sngInfo.FullName);
                if (sngFile != null)
                {
                    foreach (var stem in IniAudioChecker.SupportedStems)
                    {
                        foreach (var format in IniAudioChecker.SupportedFormats)
                        {
                            var file = stem + format;
                            if (sngFile.TryGetValue(file, out var listing))
                            {
                                streams.Add(AudioHelpers.SupportedStems[stem], sngFile.CreateStream(listing));
                                // Parse no duplicate stems
                                break;
                            }
                        }
                    }
                }
                return streams;
            }

            public byte[]? GetUnprocessedAlbumArt()
            {
                var sngFile = SngFile.TryLoadFromFile(sngInfo.FullName);
                if (sngFile == null)
                    return null;

                foreach (string albumFile in IIniMetadata.ALBUMART_FILES)
                {
                    if (sngFile.TryGetValue(albumFile, out var listing))
                    {
                        return sngFile.LoadAllBytes(listing);
                    }
                }
                return null;
            }

            public (BackgroundType Type, Stream? Stream) GetBackgroundStream(BackgroundType selections)
            {
                var sngFile = SngFile.TryLoadFromFile(sngInfo.FullName);
                if (sngFile == null)
                {
                    return (default, null);
                }

                if ((selections & BackgroundType.Yarground) > 0)
                {
                    if (sngFile.TryGetValue("bg.yarground", out var listing))
                    {
                        return (BackgroundType.Yarground, sngFile.CreateStream(listing));
                    }
                }

                if ((selections & BackgroundType.Video) > 0)
                {
                    foreach (var stem in IIniMetadata.BACKGROUND_FILENAMES)
                    {
                        foreach (var format in IIniMetadata.VIDEO_EXTENSIONS)
                        {
                            if (sngFile.TryGetValue(stem + format, out var listing))
                            {
                                return (BackgroundType.Video, sngFile.CreateStream(listing));
                            }
                        }
                    }
                }

                if ((selections & BackgroundType.Image) > 0)
                {
                    foreach (var stem in IIniMetadata.BACKGROUND_FILENAMES)
                    {
                        foreach (var format in IIniMetadata.IMAGE_EXTENSIONS)
                        {
                            if (sngFile.TryGetValue(stem + format, out var listing))
                            {
                                return (BackgroundType.Image, sngFile.CreateStream(listing));
                            }
                        }
                    }
                }

                return (default, null);
            }

            public Stream? GetPreviewAudioStream()
            {
                var sngFile = SngFile.TryLoadFromFile(sngInfo.FullName);
                if (sngFile == null)
                    return null;

                foreach (var format in IniAudioChecker.SupportedFormats)
                {
                    if (sngFile.TryGetValue("preview" + format, out var listing))
                    {
                        return sngFile.CreateStream(listing);
                    }
                }

                return null;
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

            var metadata = new SngSubmetadata(sng.Version, sngInfo, chart);

            byte[] file = sng.LoadAllBytes(sng[chart.File]);
            var result = ScanIniChartFile(file, chart.Type, sng.Metadata);

            return (result.Item1, result.Item2 != null ? new SongMetadata(metadata, result.Item2, HashWrapper.Hash(file), sng.Metadata) : null );
        }

        public static SongMetadata? SngFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            uint version = reader.Read<uint>(Endianness.Little);

            string sngPath = Path.Combine(baseDirectory, reader.ReadLEBString());
            var sngInfo = AbridgedFileInfo.TryParseInfo(sngPath, reader);
            if (sngInfo == null)
                return null;

            var sngFile = SngFile.TryLoadFromFile(sngPath);
            if (sngFile == null || sngFile.Version != version)
            {
                // TODO: Implement Update-in-place functionality
                return null;
            }

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var sngData = new SngSubmetadata(sngFile.Version, sngInfo,IIniMetadata.CHART_FILE_TYPES[chartTypeIndex]);

            return new SongMetadata(sngData, reader, strings)
            {
                _directory = sngPath
            };
        }

        public static SongMetadata? SngFromCache_Quick(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            // Implement proper versioning in the future
            uint version = reader.Read<uint>(Endianness.Little);

            string sngPath = Path.Combine(baseDirectory, reader.ReadLEBString());
            AbridgedFileInfo sngInfo = new(sngPath, DateTime.FromBinary(reader.Read<long>(Endianness.Little)));

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
            {
                YargTrace.DebugInfo("Cache file was modified externally with a bad CHART_TYPE enum value");
                return null;
            }

            var sngData = new SngSubmetadata(version, sngInfo, IIniMetadata.CHART_FILE_TYPES[chartTypeIndex]);
            return new SongMetadata(sngData, reader, strings)
            {
                _directory = sngPath
            };
        }
    }
}
