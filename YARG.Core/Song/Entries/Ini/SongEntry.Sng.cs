using System;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    internal sealed class SngEntry : IniSubEntry
    {
        private readonly uint _version;

        public override EntryType SubType => EntryType.Sng;

        internal override void Serialize(MemoryStream stream, CacheWriteIndices indices)
        {
            // Validation block
            stream.Write(_chartLastWrite.ToBinary(), Endianness.Little);
            stream.Write(_version, Endianness.Little);
            stream.WriteByte((byte) _chartFormat);

            // Metadata block
            base.Serialize(stream, indices);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            using var sngFile = SngFile.TryLoadFromFile(_location, false);
            if (!sngFile.IsLoaded)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _location);
                return null;
            }

            return CreateAudioMixer(speed, volume, sngFile, ignoreStems);
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            using var sngFile = SngFile.TryLoadFromFile(_location, false);
            if (!sngFile.IsLoaded)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _location);
                return null;
            }

            foreach (var filename in PREVIEW_FILES)
            {
                if (sngFile.TryGetListing(filename, out var listing))
                {
                    var stream = sngFile.CreateStream(filename, in listing);
                    string fakename = Path.Combine(_location, filename);
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
            using var sngFile = SngFile.TryLoadFromFile(_location, false);
            if (sngFile.IsLoaded)
            {
                if (!sngFile.TryGetListing(_cover, out var listing))
                {
                    foreach (string albumFile in ALBUMART_FILES)
                    {
                        if (sngFile.TryGetListing(albumFile, out listing))
                        {
                            break;
                        }
                    }
                }

                if (listing.Length > 0)
                {
                    using var file = sngFile.LoadAllBytes(in listing);
                    var image = YARGImage.Load(file);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogError("Failed to load SNG album art");
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground()
        {
            using var sngFile = SngFile.TryLoadFromFile(_location, false);
            if (!sngFile.IsLoaded)
            {
                return null;
            }

            if (sngFile.TryGetListing(YARGROUND_FULLNAME, out var listing))
            {
                return new BackgroundResult(BackgroundType.Yarground, sngFile.CreateStream(YARGROUND_FULLNAME, in listing));
            }

            string file = Path.ChangeExtension(_location, YARGROUND_EXTENSION);
            if (File.Exists(file))
            {
                return new BackgroundResult(BackgroundType.Yarground, File.OpenRead(file));
            }

            if (sngFile.TryGetListing(_video, out listing))
            {
                return new BackgroundResult(BackgroundType.Video, sngFile.CreateStream(_video, in listing));
            }

            foreach (var stem in BACKGROUND_FILENAMES)
            {
                foreach (var format in VIDEO_EXTENSIONS)
                {
                    string name = stem + format;
                    if (sngFile.TryGetListing(name, out listing))
                    {
                        return new BackgroundResult(BackgroundType.Video, sngFile.CreateStream(name, in listing));
                    }
                }
            }

            foreach (var format in VIDEO_EXTENSIONS)
            {
                string path = Path.ChangeExtension(_location, format);
                if (File.Exists(path))
                {
                    return new BackgroundResult(BackgroundType.Video, File.OpenRead(path));
                }
            }

            if (sngFile.TryGetListing(_background, out listing) || TryGetRandomBackgroundImage(sngFile.Listings, out listing))
            {
                using var data = sngFile.LoadAllBytes(in listing);
                var image = YARGImage.Load(data);
                if (image != null)
                {
                    return new BackgroundResult(image);
                }
                YargLogger.LogError("Failed to load SNG background image");
            }

            // Fallback to a potential external image mapped specifically to the sng
            foreach (var format in IMAGE_EXTENSIONS)
            {
                string path = Path.ChangeExtension(_location, format);
                if (File.Exists(path))
                {
                    using var data = FixedArray.LoadFile(path);
                    var image = YARGImage.Load(data);
                    if (image != null)
                    {
                        return new BackgroundResult(image);
                    }
                    YargLogger.LogFormatError("Failed to load background image {0}", path);
                }
            }
            return null;
        }

        public override FixedArray<byte>? LoadMiloData()
        {
            return null;
        }

        protected override FixedArray<byte>? GetChartData(string filename)
        {
            var data = default(FixedArray<byte>);
            if (AbridgedFileInfo.Validate(_location, _chartLastWrite))
            {
                using var sng = SngFile.TryLoadFromFile(_location, false);
                if (sng.IsLoaded && sng.TryGetListing(filename, out var listing))
                {
                    data = sng.LoadAllBytes(in listing);
                }
            }
            return data;
        }

        private StemMixer? CreateAudioMixer(float speed, double volume, in SngFile sngFile, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.ToLowerInvariant() == "yarg";
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
                {
                    continue;
                }

                foreach (var format in IniAudio.SupportedFormats)
                {
                    var file = stem + format;
                    if (sngFile.TryGetListing(file, out var listing))
                    {
                        var stream = sngFile.CreateStream(file, in listing);
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

            if (GlobalAudioHandler.LogMixerStatus)
            {
                YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
            }
            return mixer;
        }

        private SngEntry(uint version, string location, in DateTime lastWrite, ChartFormat format)
            : base(location, in lastWrite, format)
        {
            _version = version;
        }

        public static ScanExpected<SngEntry> ProcessNewEntry(in SngFile sng, in SngFileListing listing, FileInfo info, ChartFormat format, string defaultPlaylist)
        {
            var entry = new SngEntry(sng.Version, info.FullName, AbridgedFileInfo.NormalizedLastWrite(info), format);
            entry._metadata.Playlist = defaultPlaylist;

            using var file = sng.LoadAllBytes(in listing);
            var result = ScanChart(entry, file, sng.Modifiers);
            return result == ScanResult.Success ? entry : new ScanUnexpected(result);
        }

        public static SngEntry? TryDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string relative = stream.ReadString();
            string location = Path.Combine(baseDirectory, relative);
            var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            if (!AbridgedFileInfo.Validate(location, lastWrite))
            {
                return null;
            }

            uint version = stream.Read<uint>(Endianness.Little);
            if (!SngFile.ValidateMatch(location, version))
            {
                return null;
            }

            var format = CHART_FILE_TYPES[stream.ReadByte()].Format;
            var entry = new SngEntry(version, location, in lastWrite, format);
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static SngEntry ForceDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string relative = stream.ReadString();
            string location = Path.Combine(baseDirectory, relative);
            var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            uint version = stream.Read<uint>(Endianness.Little);
            var format = CHART_FILE_TYPES[stream.ReadByte()].Format;
            var entry = new SngEntry(version, location, in lastWrite, format);
            entry.Deserialize(ref stream, strings);
            return entry;
        }
    }
}
