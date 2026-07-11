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
        private readonly string? _shortname;
        public string? Shortname => _shortname;
        private readonly string? _updateMidiPath;
        internal override string? UpdateMidiPath => _updateMidiPath;
        private readonly string? _updateMoggPath;
        private readonly string? _updateImagePath;
        private RBAudio<int> _indices = RBAudio<int>.Empty;
        private RBAudio<float> _panning = RBAudio<float>.Empty;

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

            stream.Write(_shortname != null);
            if (_shortname != null)
            {
                stream.Write(_shortname);
            }

            stream.Write(_updateMidiPath != null);
            if (_updateMidiPath != null)
            {
                stream.Write(_updateMidiPath);
            }

            stream.Write(_updateMoggPath != null);
            if (_updateMoggPath != null)
            {
                stream.Write(_updateMoggPath);
            }

            stream.Write(_updateImagePath != null);
            if (_updateImagePath != null)
            {
                stream.Write(_updateImagePath);
            }

            RBCONEntry.WriteAudio(in _indices, stream);
            RBCONEntry.WriteAudio(in _panning, stream);

            base.Serialize(stream, node);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            if (_updateMoggPath != null && File.Exists(_updateMoggPath))
            {
                var moggMixer = LoadUpdateMoggAudio(speed, volume, ignoreStems);
                if (moggMixer != null)
                {
                    return moggMixer;
                }
                YargLogger.LogFormatError("Update mogg at {0} failed to load, falling back to loose audio files", _updateMoggPath);
            }
            return LoadLooseAudio(speed, volume, ignoreStems);
        }

        private StemMixer? LoadUpdateMoggAudio(float speed, double volume, SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume: clampStemVolume,
                normalize: true);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer!");
                return null;
            }

            var stream = new FileStream(_updateMoggPath!, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            int version = stream.Read<int>(Endianness.Little);
            if (version != RBCONEntry.UNENCRYPTED_MOGG)
            {
                YargLogger.LogError("Encrypted update moggs are not supported!");
                stream.Dispose();
                mixer.Dispose();
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            if (!RBCONEntry.AddMoggStems(mixer, stream, in _indices, in _panning, ignoreStems))
            {
                stream.Dispose();
                mixer.Dispose();
                return null;
            }

            if (GlobalAudioHandler.LogMixerStatus)
            {
                YargLogger.LogFormatInfo("Loaded {0} stems from update mogg", mixer.Channels.Count);
            }
            return mixer;
        }

        private StemMixer? LoadLooseAudio(float speed, double volume, SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume: clampStemVolume,
                normalize: true);
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
                        if (mixer.AddChannel(stream, stemEnum))
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
                    return GlobalAudioHandler.LoadCustomFile(audioFile, speed, 0, true, SongStem.Preview);
                }
            }
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            if (_updateImagePath != null && File.Exists(_updateImagePath))
            {
                var updateImage = YARGImage.LoadDXT(_updateImagePath);
                if (updateImage != null)
                {
                    return updateImage;
                }
                YargLogger.LogFormatError("Update image at {0} failed to load", _updateImagePath);
            }

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

        public override BackgroundResult? LoadBackground(bool excludeYarground = false)
        {
            var subFiles = GetSubFiles();
            if (subFiles.TryGetValue("bg.yarground", out var file) && !excludeYarground)
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

        #nullable disable
        public override FixedArray<byte> LoadMiloData()
        {
            var subFiles = GetSubFiles();
            foreach (var name in subFiles.Keys)
            {
                if (name.EndsWith(".milo_xbox") || name.EndsWith(".milo"))
                {
                    if (subFiles.TryGetValue(name, out var file) && File.Exists(file))
                    {
                        return FixedArray.LoadFile(file);
                    }
                }
            }

            return null;
        }

        protected override FixedArray<byte> GetChartData(string filename)
        {
            string chartPath = Path.Combine(_location, filename);
            if (!AbridgedFileInfo.Validate(chartPath, in _chartLastWrite))
            {
                return null;
            }

            string iniPath = Path.Combine(_location, "song.ini");
            if (_iniLastWrite.HasValue)
            {
                if (!AbridgedFileInfo.Validate(iniPath, _iniLastWrite.Value) && File.Exists(iniPath))
                {
                    return null;
                }
            }
            else if (File.Exists(iniPath))
            {
                return null;
            }

            return FixedArray.LoadFile(chartPath);
        }
        #nullable restore

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

        private UnpackedIniEntry(string directory, in DateTime chartLastWrite, in DateTime? iniLastWrite, in ChartFormat format,
            string? shortname, string? updateMidiPath, string? updateMoggPath, string? updateImagePath)
            : base(directory, in chartLastWrite, format)
        {
            _iniLastWrite = iniLastWrite;
            _shortname = shortname;
            _updateMidiPath = updateMidiPath;
            _updateMoggPath = updateMoggPath;
            _updateImagePath = updateImagePath;
        }

        public static ScanExpected<UnpackedIniEntry> ProcessNewEntry(string directory, FileInfo chartInfo, ChartFormat format, FileInfo? iniFile, string defaultPlaylist, IReadOnlyDictionary<string, IniUpdateInfo> iniUpdateInfos)
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

            string? shortname = iniModifiers.Extract("shortname", out string sn) ? sn : null;
            string? updateMidiPath = null;
            string? updateMoggPath = null;
            string? updateImagePath = null;
            var indices = RBAudio<int>.Empty;
            var panning = RBAudio<float>.Empty;
            if (shortname != null && iniUpdateInfos.TryGetValue(shortname, out var updateInfo))
            {
                updateMidiPath = updateInfo.MidiPath;
                updateMoggPath = updateInfo.MoggPath;
                updateImagePath = updateInfo.ImagePath;
                RBAudioCalculator.Calculate(in updateInfo.Dta, ref indices, ref panning);
            }

            var entry = new UnpackedIniEntry(directory, AbridgedFileInfo.NormalizedLastWrite(chartInfo), in iniLastWrite, format,
                shortname, updateMidiPath, updateMoggPath, updateImagePath)
            {
                _indices = indices,
                _panning = panning,
            };
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

            string? shortname = stream.ReadBoolean() ? stream.ReadString() : null;
            string? updateMidiPath = stream.ReadBoolean() ? stream.ReadString() : null;
            if (updateMidiPath != null && !File.Exists(updateMidiPath))
            {
                return null; // update mid vanished since cache was written — force rescan
            }

            string? updateMoggPath = stream.ReadBoolean() ? stream.ReadString() : null;
            if (updateMoggPath != null && !File.Exists(updateMoggPath))
            {
                return null; // update mogg vanished since cache was written — force rescan
            }

            string? updateImagePath = stream.ReadBoolean() ? stream.ReadString() : null;
            if (updateImagePath != null && !File.Exists(updateImagePath))
            {
                return null; // update image vanished since cache was written — force rescan
            }

            var indices = RBAudio<int>.Empty;
            var panning = RBAudio<float>.Empty;
            RBCONEntry.ReadAudio(ref indices, ref stream);
            RBCONEntry.ReadAudio(ref panning, ref stream);

            var entry = new UnpackedIniEntry(directory, in chartLastWrite, in iniLastWrite, chart.Format, shortname, updateMidiPath, updateMoggPath, updateImagePath)
            {
                _indices = indices,
                _panning = panning,
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static UnpackedIniEntry ForceDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = Path.Combine(baseDirectory, stream.ReadString());
            ref readonly var chart = ref CHART_FILE_TYPES[stream.ReadByte()];
            var chartLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            DateTime? iniLastWrite = stream.ReadBoolean() ? DateTime.FromBinary(stream.Read<long>(Endianness.Little)) : default;
            string? shortname = stream.ReadBoolean() ? stream.ReadString() : null;
            string? updateMidiPath = stream.ReadBoolean() ? stream.ReadString() : null;
            string? updateMoggPath = stream.ReadBoolean() ? stream.ReadString() : null;
            string? updateImagePath = stream.ReadBoolean() ? stream.ReadString() : null;

            var indices = RBAudio<int>.Empty;
            var panning = RBAudio<float>.Empty;
            RBCONEntry.ReadAudio(ref indices, ref stream);
            RBCONEntry.ReadAudio(ref panning, ref stream);

            var entry = new UnpackedIniEntry(directory, in chartLastWrite, in iniLastWrite, chart.Format, shortname, updateMidiPath, updateMoggPath, updateImagePath)
            {
                _indices = indices,
                _panning = panning,
            };
            entry.Deserialize(ref stream, strings);
            return entry;
        }
    }
}
