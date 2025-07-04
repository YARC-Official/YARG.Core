using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Audio;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Logging;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    internal sealed class UnpackedIniEntry : IniSubEntry
    {
        private readonly DateTime? _iniLastWrite;

        public override EntryType SubType => EntryType.Ini;

        internal override void Serialize(MemoryStream stream, CacheWriteIndices node)
        {
            stream.WriteByte((byte) _chartFormat);
            stream.Write(_chartLastWrite.ToBinary(), Endianness.Little);
            stream.Write(_iniLastWrite.HasValue);
            if (_iniLastWrite.HasValue)
            {
                stream.Write(_iniLastWrite.Value.ToBinary(), Endianness.Little);
            }
            base.Serialize(stream, node);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer!");
                return null;
            }

            var subFiles = GetSubFiles();
            foreach (var stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                if (ignoreStems.Contains(stemEnum))
                    continue;

                foreach (var format in IniAudio.SupportedFormats)
                {
                    var stemName = stem + format;
                    if (subFiles.TryGetValue(stemName, out var file))
                    {
                        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
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

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            foreach (var filename in PREVIEW_FILES)
            {
                var audioFile = Path.Combine(_location, filename);
                if (File.Exists(audioFile))
                {
                    return GlobalAudioHandler.LoadCustomFile(audioFile, speed, 0, SongStem.Preview);
                }
            }
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            var subFiles = GetSubFiles();
            if (!string.IsNullOrEmpty(_cover) && subFiles.TryGetValue(_cover, out var cover))
            {
                var image = YARGImage.Load(cover);
                if (image != null)
                {
                    return image;
                }
                YargLogger.LogFormatError("Image at {0} failed to load", cover);
            }

            foreach (string albumName in ALBUMART_FILES)
            {
                if (subFiles.TryGetValue(albumName, out var file))
                {
                    var image = YARGImage.Load(file);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("Image at {0} failed to load", file);
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground()
        {
            var subFiles = GetSubFiles();
            if (subFiles.TryGetValue("bg.yarground", out var file))
            {
                var stream = File.OpenRead(file);
                return new BackgroundResult(BackgroundType.Yarground, stream);
            }

            if (subFiles.TryGetValue(_video, out var video))
            {
                var stream = File.OpenRead(video);
                return new BackgroundResult(BackgroundType.Video, stream);
            }

            foreach (var stem in BACKGROUND_FILENAMES)
            {
                foreach (var format in VIDEO_EXTENSIONS)
                {
                    if (subFiles.TryGetValue(stem + format, out file))
                    {
                        var stream = File.OpenRead(file);
                        return new BackgroundResult(BackgroundType.Video, stream);
                    }
                }
            }

            if (subFiles.TryGetValue(_background, out file) || TryGetRandomBackgroundImage(subFiles, out file))
            {
                var image = YARGImage.Load(file!);
                if (image != null)
                {
                    return new BackgroundResult(image);
                }
            }
            return null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
            return null;
        }

        protected override FixedArray<byte> GetChartData(string filename)
        {
            var data = default(FixedArray<byte>);

            string chartPath = Path.Combine(_location, filename);
            if (AbridgedFileInfo.Validate(chartPath, in _chartLastWrite))
            {
                string iniPath = Path.Combine(_location, "song.ini");
                if (_iniLastWrite.HasValue
                    ? AbridgedFileInfo.Validate(iniPath, _iniLastWrite.Value)
                    : !File.Exists(iniPath))
                {
                    data = FixedArray.LoadFile(chartPath);
                }
            }
            return data;
        }

        private Dictionary<string, string> GetSubFiles()
        {
            Dictionary<string, string> files = new();
            if (Directory.Exists(_location))
            {
                foreach (var file in Directory.EnumerateFiles(_location))
                {
                    files.Add(file[(_location.Length + 1)..].ToLower(), file);
                }
            }
            return files;
        }

        private UnpackedIniEntry(string directory, in DateTime chartLastWrite, in DateTime? iniLastWrite, in ChartFormat format)
            : base(directory, in chartLastWrite, format)
        {
            _iniLastWrite = iniLastWrite;
        }

        public static ScanExpected<UnpackedIniEntry> ProcessNewEntry(string directory, FileInfo chartInfo, ChartFormat format, FileInfo? iniFile, string defaultPlaylist)
        {
            IniModifierCollection iniModifiers;
            DateTime? iniLastWrite = default;
            if (iniFile != null)
            {
                iniModifiers = SongIniHandler.ReadSongIniFile(iniFile.FullName);
                iniLastWrite = AbridgedFileInfo.NormalizedLastWrite(iniFile);
            }
            else
            {
                iniModifiers = new();
            }

            var entry = new UnpackedIniEntry(directory, AbridgedFileInfo.NormalizedLastWrite(chartInfo), in iniLastWrite, format);
            entry._metadata.Playlist = defaultPlaylist;

            using var file = FixedArray.LoadFile(chartInfo.FullName);

            var result = ScanChart(entry, file, iniModifiers);
            return result == ScanResult.Success ? entry : new ScanUnexpected(result);
        }

        public static UnpackedIniEntry? TryDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = Path.Combine(baseDirectory, stream.ReadString());
            ref readonly var chart = ref CHART_FILE_TYPES[stream.ReadByte()];
            var chartLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            if (!AbridgedFileInfo.Validate(Path.Combine(directory, chart.Filename), chartLastWrite))
            {
                return null;
            }

            string iniFile = Path.Combine(directory, "song.ini");
            DateTime? iniLastWrite = default;
            if (stream.ReadBoolean())
            {
                iniLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                if (!AbridgedFileInfo.Validate(iniFile, iniLastWrite.Value))
                {
                    return null;
                }
            }
            else if (File.Exists(iniFile))
            {
                return null;
            }

            var entry = new UnpackedIniEntry(directory, in chartLastWrite, in iniLastWrite, chart.Format);
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static UnpackedIniEntry ForceDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = Path.Combine(baseDirectory, stream.ReadString());
            ref readonly var chart = ref CHART_FILE_TYPES[stream.ReadByte()];
            var chartLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            DateTime? iniLastWrite = stream.ReadBoolean() ? DateTime.FromBinary(stream.Read<long>(Endianness.Little)) : default;
            var entry = new UnpackedIniEntry(directory, in chartLastWrite, in iniLastWrite, chart.Format);
            entry.Deserialize(ref stream, strings);
            return entry;
        }
    }
}
