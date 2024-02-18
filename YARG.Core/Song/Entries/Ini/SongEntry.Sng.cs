using System;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public sealed class SngEntry : IniSubEntry
    {
        private readonly uint _version;
        private readonly AbridgedFileInfo _sngInfo;
        private readonly IniChartNode<string> _chart;

        public override string Directory => _sngInfo.FullName;
        public override ChartType Type => _chart.Type;
        public override DateTime GetAddTime() => _sngInfo.LastUpdatedTime;

        public override EntryType SubType => EntryType.Sng;

        protected override void SerializeSubData(BinaryWriter writer)
        {
            writer.Write(_sngInfo.LastUpdatedTime.ToBinary());
            writer.Write(_version);
            writer.Write((byte) _chart.Type);
        }

        protected override Stream? GetChartStream()
        {
            if (!_sngInfo.IsStillValid())
                return null;

            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
                return null;

            return sngFile[_chart.File].CreateStream(sngFile);
        }

        public override AudioMixer? LoadAudioStreams(params SongStem[] ignoreStems)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                return null;
            }
            return CreateAudioMixer(sngFile, ignoreStems);
        }

        public override byte[]? LoadAlbumData()
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
                return null;

            if (!string.IsNullOrEmpty(_cover) && sngFile.TryGetValue(_video, out var cover))
            {
                return cover.LoadAllBytes(sngFile);
            }

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (sngFile.TryGetValue(albumFile, out var listing))
                {
                    return listing.LoadAllBytes(sngFile);
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                return null;
            }

            if ((options & BackgroundType.Yarground) > 0)
            {
                if (sngFile.TryGetValue("bg.yarground", out var listing))
                {
                    return new(BackgroundType.Yarground, listing.CreateStream(sngFile));
                }

                // Try to find a yarground mapped to the specific .sng
                string filepath = Path.ChangeExtension(_sngInfo.FullName, YARGROUND_EXTENSION);
                if (File.Exists(filepath))
                {
                    var stream = File.OpenRead(filepath);
                    return new(BackgroundType.Yarground, stream);
                }

                // Otherwise, randomly select one that is present in the same folder
                string directory = Path.GetDirectoryName(_sngInfo.FullName);
                var venue = SelectRandomYarground(directory);
                if (venue != null)
                {
                    return venue;
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (!string.IsNullOrEmpty(_video) && sngFile.TryGetValue(_video, out var video))
                {
                    return new(BackgroundType.Video, video.CreateStream(sngFile));
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (sngFile.TryGetValue(stem + format, out var listing))
                        {
                            return new (BackgroundType.Video, listing.CreateStream(sngFile));
                        }
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (!string.IsNullOrEmpty(_background) && sngFile.TryGetValue(_background, out var background))
                {
                    return new(BackgroundType.Image, background.CreateStream(sngFile));
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in IMAGE_EXTENSIONS)
                    {
                        if (sngFile.TryGetValue(stem + format, out var listing))
                        {
                            return new(BackgroundType.Image, listing.CreateStream(sngFile));
                        }
                    }
                }
            }

            return null;
        }

        public override AudioMixer? LoadPreviewAudio()
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                return null;
            }

            foreach (var format in IniAudioChecker.SupportedFormats)
            {
                if (sngFile.TryGetValue("preview" + format, out var listing))
                {
                    var mixer = new AudioMixer();
                    var channel = new AudioChannel(SongStem.Preview, listing.CreateStream(sngFile));
                    mixer.Channels.Add(channel);
                    return mixer;
                }
            }

            return CreateAudioMixer(sngFile, SongStem.Crowd);
        }

        private AudioMixer CreateAudioMixer(SngFile sngFile, params SongStem[] ignoreStems)
        {
            var mixer = new AudioMixer();
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
                        var channel = new AudioChannel(stemEnum, listing.CreateStream(sngFile));
                        mixer.Channels.Add(channel);
                        // Parse no duplicate stems
                        break;
                    }
                }
            }
            return mixer;
        }

        private SngEntry(SngFile sngFile, IniChartNode<string> chart, in AvailableParts parts, HashWrapper hash, IniSection modifiers, string defaultPlaylist)
            : base(in parts, in hash, modifiers, defaultPlaylist)
        {
            _version = sngFile.Version;
            _sngInfo = sngFile.Info;
            _chart = chart;
        }

        private SngEntry(uint version, AbridgedFileInfo sngInfo, IniChartNode<string> chart, BinaryReader reader, CategoryCacheStrings strings)
            : base(reader, strings)
        {
            _version = version;
            _sngInfo = sngInfo;
            _chart = chart;
        }

        public static (ScanResult, SngEntry?) ProcessNewEntry(SngFile sng, IniChartNode<string> chart, string defaultPlaylist)
        {
            byte[] file = sng[chart.File].LoadAllBytes(sng);
            var (result, parts) = ScanIniChartFile(file, chart.Type, sng.Metadata);
            if (result != ScanResult.Success)
            {
                return (result, null);
            }

            var entry = new SngEntry(sng, chart, in parts, HashWrapper.Hash(file), sng.Metadata, defaultPlaylist);
            return (result, entry);
        }

        public static IniSubEntry? TryLoadFromCache(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            string sngPath = Path.Combine(baseDirectory, reader.ReadString());
            var sngInfo = AbridgedFileInfo.TryParseInfo(sngPath, reader);
            if (sngInfo == null)
                return null;

            uint version = reader.ReadUInt32();
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
            return new SngEntry(sngFile.Version, sngInfo, CHART_FILE_TYPES[chartTypeIndex], reader, strings);
        }

        public static IniSubEntry? LoadFromCache_Quick(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            string sngPath = Path.Combine(baseDirectory, reader.ReadString());
            AbridgedFileInfo sngInfo = new(sngPath, DateTime.FromBinary(reader.ReadInt64()));

            uint version = reader.ReadUInt32();
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }
            return new SngEntry(version, sngInfo, CHART_FILE_TYPES[chartTypeIndex], reader, strings);
        }
    }
}
