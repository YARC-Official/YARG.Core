using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public sealed class SngMetadata : IniSubMetadata
    {
        private readonly uint version;
        private readonly AbridgedFileInfo sngInfo;
        private readonly IniChartNode<string> chart;

        public override string Directory => sngInfo.FullName;
        public override ChartType Type => chart.Type;
        public override DateTime GetAddTime() => sngInfo.LastUpdatedTime;

        public override void Serialize(BinaryWriter writer, string groupDirectory)
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

        protected override Stream? GetChartStream()
        {
            if (!sngInfo.IsStillValid())
                return null;

            var sngFile = SngFile.TryLoadFromFile(sngInfo);
            if (sngFile == null)
                return null;

            return sngFile[chart.File].CreateStream(sngFile);
        }

        protected override Dictionary<SongStem, Stream> GetAudioStreams(params SongStem[] ignoreStems)
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

        protected override byte[]? GetUnprocessedAlbumArt()
        {
            var sngFile = SngFile.TryLoadFromFile(sngInfo);
            if (sngFile == null)
                return null;

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (sngFile.TryGetValue(albumFile, out var listing))
                {
                    return listing.LoadAllBytes(sngFile);
                }
            }
            return null;
        }

        protected override (BackgroundType Type, Stream? Stream) GetBackgroundStream(BackgroundType selections)
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
                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
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
                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in IMAGE_EXTENSIONS)
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

        protected override Stream? GetPreviewAudioStream()
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

        private SngMetadata(uint version, AbridgedFileInfo sngInfo, IniChartNode<string> chart
            , AvailableParts parts, HashWrapper hash, IniSection section, string defaultPlaylist)
            : base(parts, hash, section, defaultPlaylist)
        {
            this.version = version;
            this.sngInfo = sngInfo;
            this.chart = chart;
        }

        private SngMetadata(uint version, AbridgedFileInfo sngInfo, IniChartNode<string> chart
            , BinaryReader reader, CategoryCacheStrings strings)
            : base(reader, strings)
        {
            this.version = version;
            this.sngInfo = sngInfo;
            this.chart = chart;
        }

        public static (ScanResult, SngMetadata?) ProcessNewEntry(SngFile sng, IniChartNode<string> chart, string defaultPlaylist)
        {
            byte[] file = sng[chart.File].LoadAllBytes(sng);
            var result = ScanIniChartFile(file, chart.Type, sng.Metadata);
            if (result.Item2 == null)
            {
                return (result.Item1, null);
            }

            var metadata = new SngMetadata(sng.Version, sng.Info, chart, result.Item2, HashWrapper.Hash(file), sng.Metadata, defaultPlaylist);
            return (result.Item1, metadata);
        }

        public static IniSubMetadata? TryLoadFromCache(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            uint version = reader.ReadUInt32();

            string sngPath = Path.Combine(baseDirectory, reader.ReadString());
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
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }
            return new SngMetadata(sngFile.Version, sngInfo, CHART_FILE_TYPES[chartTypeIndex], reader, strings);
        }

        public static IniSubMetadata? LoadFromCache_Quick(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            // Implement proper versioning in the future
            uint version = reader.ReadUInt32();

            string sngPath = Path.Combine(baseDirectory, reader.ReadString());
            AbridgedFileInfo sngInfo = new(sngPath, DateTime.FromBinary(reader.ReadInt64()));

            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }
            return new SngMetadata(version, sngInfo, CHART_FILE_TYPES[chartTypeIndex], reader, strings);
        }
    }
}
