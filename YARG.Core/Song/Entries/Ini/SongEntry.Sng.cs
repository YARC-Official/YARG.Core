using System;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Logging;
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

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _sngInfo.FullName);
                return null;
            }
            return CreateAudioMixer(speed, volume, sngFile, ignoreStems);
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _sngInfo.FullName);
                return null;
            }

            foreach (var filename in PREVIEW_FILES)
            {
                if (sngFile.TryGetValue(filename, out var listing))
                {
                    string fakename = Path.Combine(_sngInfo.FullName, filename);
                    var stream = listing.CreateStream(sngFile);
                    var mixer = GlobalAudioHandler.LoadCustomFile(fakename, stream, speed, 0, SongStem.Preview);
                    if (mixer == null)
                    {
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load preview file {0}!", fakename);
                        return null;
                    }
                    return mixer;
                }
            }

            return CreateAudioMixer(speed, 0, sngFile, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
                return null;

            if (!string.IsNullOrEmpty(_cover) && sngFile.TryGetValue(_video, out var cover))
            {
                var image = YARGImage.Load(cover, sngFile);
                if (image != null)
                {
                    return image;
                }
                YargLogger.LogFormatError("SNG Image mapped to {0} failed to load", cover.Name);
            }

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (sngFile.TryGetValue(albumFile, out var listing))
                {
                    var image = YARGImage.Load(listing, sngFile);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("SNG Image mapped to {0} failed to load", listing.Name);
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
                if (sngFile.TryGetValue(YARGROUND_FULLNAME, out var listing))
                {
                    return new BackgroundResult(BackgroundType.Yarground, listing.CreateStream(sngFile));
                }

                string file = Path.ChangeExtension(_sngInfo.FullName, YARGROUND_EXTENSION);
                if (File.Exists(file))
                {
                    var stream = File.OpenRead(file);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (!string.IsNullOrEmpty(_video) && sngFile.TryGetValue(_video, out var video))
                {
                    return new BackgroundResult(BackgroundType.Video, video.CreateStream(sngFile));
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (sngFile.TryGetValue(stem + format, out var listing))
                        {
                            return new BackgroundResult(BackgroundType.Video, listing.CreateStream(sngFile));
                        }
                    }
                }

                foreach (var format in VIDEO_EXTENSIONS)
                {
                    string file = Path.ChangeExtension(_sngInfo.FullName, format);
                    if (File.Exists(file))
                    {
                        var stream = File.OpenRead(file);
                        return new BackgroundResult(BackgroundType.Video, stream);
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (!string.IsNullOrEmpty(_background) && sngFile.TryGetValue(_background, out var background))
                {
                    return new BackgroundResult(BackgroundType.Image, background.CreateStream(sngFile));
                }

                var listing = GetRandomBackgroundImage(sngFile);
                if (listing != null)
                {
                    return new BackgroundResult(BackgroundType.Image, listing.CreateStream(sngFile));
                }

                // Fallback to a potential external image mapped specifically to the sng
                foreach (var format in IMAGE_EXTENSIONS)
                {
                    string file = Path.ChangeExtension(_sngInfo.FullName, format);
                    if (File.Exists(file))
                    {
                        var stream = File.OpenRead(file);
                        return new BackgroundResult(BackgroundType.Image, stream);
                    }
                }
            }

            return null;
        }

        private StemMixer? CreateAudioMixer(float speed, double volume, SngFile sngFile, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.Str.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer");
                return null;
            }

            foreach (var stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                if (ignoreStems.Contains(stemEnum))
                    continue;

                foreach (var format in IniAudio.SupportedFormats)
                {
                    var file = stem + format;
                    if (sngFile.TryGetValue(file, out var listing))
                    {
                        var stream = listing.CreateStream(sngFile);
                        if (mixer.AddChannel(stemEnum, stream))
                        {
                            // No duplicates
                            break;
                        }
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load stem file {0}", file);
                    }
                }
            }

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Failed to add any stems!");
                mixer.Dispose();
                return null;
            }
            YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
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
            if (!sng.Metadata.Contains("song_length"))
            {
                using var mixer = entry.LoadAudio(0, 0);
                if (mixer != null)
                {
                    entry.SongLengthSeconds = mixer.Length;
                }
            }
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
