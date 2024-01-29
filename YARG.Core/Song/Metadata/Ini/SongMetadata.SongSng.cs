using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            private readonly IniChartNode<string> chart;

            public string Root => sngInfo.FullName;
            public ChartType Type => chart.Type;

            public SngSubmetadata(uint version, AbridgedFileInfo sngInfo, IniChartNode<string> chart)
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
                writer.Write(sngInfo.LastUpdatedTime.ToBinary());
                writer.Write((byte) chart.Type);
            }

            public Stream? GetChartStream()
            {
                if (!sngInfo.IsStillValid())
                    return null;

                var sngFile = SngFile.TryLoadFromFile(sngInfo);
                if (sngFile == null)
                    return null;

                return sngFile[chart.File].CreateStream(sngFile);
            }

            public Dictionary<SongStem, Stream> GetAudioStreams(params SongStem[] ignoreStems)
            {
                Dictionary<SongStem, Stream> streams = new();

                var sngFile = SngFile.TryLoadFromFile(sngInfo);
                if (sngFile != null)
                {
                    foreach (var stem in IniAudioChecker.SupportedStems)
                    {
                        var stemEnum = AudioHelpers.SupportedStems[stem];
                        if (ignoreStems.Contains(stemEnum))
                            continue;

                        foreach (var format in IniAudioChecker.SupportedFormats)
                        {
                            var file = stem + format;
                            if (sngFile.TryGetValue(file, out var listing))
                            {
                                streams.Add(stemEnum, listing.CreateStream(sngFile));
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
                var sngFile = SngFile.TryLoadFromFile(sngInfo);
                if (sngFile == null)
                    return null;

                foreach (string albumFile in IIniMetadata.ALBUMART_FILES)
                {
                    if (sngFile.TryGetValue(albumFile, out var listing))
                    {
                        return listing.LoadAllBytes(sngFile);
                    }
                }
                return null;
            }

            public (BackgroundType Type, Stream? Stream) GetBackgroundStream(BackgroundType selections)
            {
                var sngFile = SngFile.TryLoadFromFile(sngInfo);
                if (sngFile == null)
                {
                    return (default, null);
                }

                if ((selections & BackgroundType.Yarground) > 0)
                {
                    if (sngFile.TryGetValue("bg.yarground", out var listing))
                    {
                        return (BackgroundType.Yarground, listing.CreateStream(sngFile));
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
                                return (BackgroundType.Video, listing.CreateStream(sngFile));
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
                                return (BackgroundType.Image, listing.CreateStream(sngFile));
                            }
                        }
                    }
                }

                return (default, null);
            }

            public Stream? GetPreviewAudioStream()
            {
                var sngFile = SngFile.TryLoadFromFile(sngInfo);
                if (sngFile == null)
                    return null;

                foreach (var format in IniAudioChecker.SupportedFormats)
                {
                    if (sngFile.TryGetValue("preview" + format, out var listing))
                    {
                        return listing.CreateStream(sngFile);
                    }
                }

                return null;
            }
        }

        public static (ScanResult, SongMetadata?) FromSng(SngFile sng, IniChartNode<string> chart, string defaultPlaylist)
        {
            byte[] file = sng[chart.File].LoadAllBytes(sng);
            var result = ScanIniChartFile(file, chart.Type, sng.Metadata);
            if (result.Item2 == null)
            {
                return (result.Item1, null);
            }

            var packed = new SngSubmetadata(sng.Version, sng.Info, chart);
            var metadata = new SongMetadata(packed, result.Item2, HashWrapper.Hash(file), sng.Metadata, defaultPlaylist);
            return (result.Item1, metadata);
        }

        public static SongMetadata? SngFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            uint version = reader.Read<uint>(Endianness.Little);

            string sngPath = Path.Combine(baseDirectory, reader.ReadLEBString());
            var sngInfo = AbridgedFileInfo.TryParseInfo(sngPath, reader);
            if (sngInfo == null)
                return null;

            var sngFile = SngFile.TryLoadFromFile(sngInfo);
            if (sngFile == null || sngFile.Version != version)
            {
                // TODO: Implement Update-in-place functionality
                return null;
            }

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var sngData = new SngSubmetadata(sngFile.Version, sngInfo, IIniMetadata.CHART_FILE_TYPES[chartTypeIndex]);

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
