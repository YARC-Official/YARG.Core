﻿using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Audio;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    public sealed class UnpackedIniEntry : IniSubEntry
    {
        private readonly AbridgedFileInfo _chartFile;
        private readonly AbridgedFileInfo? _iniFile;

        public override string Directory { get; }
        public override ChartType Type { get; }
        public override DateTime GetAddTime() => _chartFile.LastUpdatedTime;

        public override EntryType SubType => EntryType.Ini;

        protected override void SerializeSubData(BinaryWriter writer)
        {
            writer.Write((byte) Type);
            writer.Write(_chartFile.LastUpdatedTime.ToBinary());
            if (_iniFile != null)
            {
                writer.Write(true);
                writer.Write(_iniFile.LastUpdatedTime.ToBinary());
            }
            else
                writer.Write(false);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = _metadata.Source.Str.ToLowerInvariant() == "yarg";
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
                    var audioFile = stem + format;
                    if (subFiles.TryGetValue(audioFile, out var fullname))
                    {
                        var stream = new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stemEnum, stream))
                        {
                            // No duplicates
                            break;
                        }
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load stem file {0}", fullname);
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

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            foreach (var filename in PREVIEW_FILES)
            {
                var audioFile = Path.Combine(Directory, filename);
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

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (subFiles.TryGetValue(albumFile, out var fullname))
                {
                    var image = YARGImage.Load(fullname);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("Image at {0} failed to load", fullname);
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            var subFiles = GetSubFiles();
            if ((options & BackgroundType.Yarground) > 0)
            {
                if (subFiles.TryGetValue("bg.yarground", out var file))
                {
                    var stream = File.OpenRead(file);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (!string.IsNullOrEmpty(_video) && subFiles.TryGetValue(_video, out var video))
                {
                    var stream = File.OpenRead(video);
                    return new BackgroundResult(BackgroundType.Video, stream);
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (subFiles.TryGetValue(stem + format, out var fullname))
                        {
                            var stream = File.OpenRead(fullname);
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (string.IsNullOrEmpty(_background) || !subFiles.TryGetValue(_background, out var file))
                {
                    file = GetRandomBackgroundImage(subFiles);
                }

                if (file != null)
                {
                    var image = YARGImage.Load(file);
                    if (image != null)
                    {
                        return new BackgroundResult(image);
                    }
                }
            }
            return null;
        }

        protected override Stream? GetChartStream()
        {
            if (!_chartFile.IsStillValid())
                return null;

            if (_iniFile != null)
            {
                if (!_iniFile.IsStillValid())
                    return null;
            }
            else if (File.Exists(Path.Combine(Directory, "song.ini")))
            {
                return null;
            }

            return new FileStream(_chartFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

        private Dictionary<string, string> GetSubFiles()
        {
            Dictionary<string, string> files = new();
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(Directory))
                {
                    files.Add(Path.GetFileName(file).ToLower(), file);
                }
            }
            return files;
        }

        private UnpackedIniEntry(string directory, ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile, in AvailableParts parts, HashWrapper hash, IniSection modifiers, string defaultPlaylist)
            : base(in parts, in hash, modifiers, defaultPlaylist)
        {
            Directory = directory;
            Type = chartType;
            _chartFile = chartFile;
            _iniFile = iniFile;
        }

        private UnpackedIniEntry(string directory, ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile, BinaryReader reader, CategoryCacheStrings strings)
            : base(reader, strings)
        {
            Directory = directory;
            Type = chartType;
            _chartFile = chartFile;
            _iniFile = iniFile;
        }

        public static (ScanResult, UnpackedIniEntry?) ProcessNewEntry(string chartDirectory, IniChartNode<FileInfo> chart, FileInfo? iniFile, string defaultPlaylist)
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
            var (result, parts) = ScanIniChartFile(file, chart.Type, iniModifiers);
            if (result != ScanResult.Success)
            {
                return (result, null);
            }

            var abridged = new AbridgedFileInfo(chart.File);
            var entry = new UnpackedIniEntry(chartDirectory, chart.Type, abridged, iniFileInfo, in parts, HashWrapper.Hash(file), iniModifiers, defaultPlaylist);
            if (!iniModifiers.Contains("song_length"))
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
            string directory = Path.Combine(baseDirectory, reader.ReadString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = CHART_FILE_TYPES[chartTypeIndex];
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
            return new UnpackedIniEntry(directory, chart.Type, chartInfo, iniInfo, reader, strings);
        }

        public static IniSubEntry? IniFromCache_Quick(string baseDirectory, BinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = CHART_FILE_TYPES[chartTypeIndex];
            var lastUpdated = DateTime.FromBinary(reader.ReadInt64());

            var chartInfo = new AbridgedFileInfo(Path.Combine(directory, chart.File), lastUpdated);
            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                lastUpdated = DateTime.FromBinary(reader.ReadInt64());
                iniInfo = new AbridgedFileInfo(Path.Combine(directory, "song.ini"), lastUpdated);
            }
            return new UnpackedIniEntry(directory, chart.Type, chartInfo, iniInfo, reader, strings);
        }
    }
}
