﻿using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Audio;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Extensions;

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

        public override List<AudioChannel> LoadAudioStreams(params SongStem[] ignoreStems)
        {
            Dictionary<string, string> files = new();
            {
                var parsed = System.IO.Directory.GetFiles(Directory);
                foreach (var file in parsed)
                    files.Add(Path.GetFileName(file).ToLower(), file);
            }

            var channels = new List<AudioChannel>();
            foreach (var stem in IniAudioChecker.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                if (ignoreStems.Contains(stemEnum))
                    continue;

                foreach (var format in IniAudioChecker.SupportedFormats)
                {
                    var audioFile = stem + format;
                    if (files.TryGetValue(audioFile, out var fullname))
                    {
                        // No file buffer
                        var channel = new AudioChannel(stemEnum, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read, 1));
                        channels.Add(channel);
                        // Parse no duplicate stems
                        break;
                    }
                }
            }
            return channels;
        }

        public override byte[]? LoadAlbumData()
        {
            Dictionary<string, string> files = new();
            {
                var parsed = System.IO.Directory.GetFiles(Directory);
                foreach (var file in parsed)
                    files.Add(Path.GetFileName(file).ToLower(), file);
            }

            foreach (string albumFile in ALBUMART_FILES)
                if (files.TryGetValue(albumFile, out var fullname))
                    return File.ReadAllBytes(fullname);
            return null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            Dictionary<string, string> files = new();
            {
                var parsed = System.IO.Directory.GetFiles(Directory);
                foreach (var file in parsed)
                    files.Add(Path.GetFileName(file).ToLower(), file);
            }

            if ((options & BackgroundType.Yarground) > 0)
            {
                if (files.TryGetValue("bg.yarground", out var file))
                    return new BackgroundResult(BackgroundType.Yarground, new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            }

            if ((options & BackgroundType.Video) > 0)
            {
                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (files.TryGetValue(stem + format, out var fullname))
                            return new BackgroundResult(BackgroundType.Video, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read));
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in IMAGE_EXTENSIONS)
                    {
                        if (files.TryGetValue(stem + format, out var fullname))
                            return new BackgroundResult(BackgroundType.Image, new FileStream(fullname, FileMode.Open, FileAccess.Read, FileShare.Read));
                    }
                }
            }
            return null;
        }

        public override List<AudioChannel> LoadPreviewAudio()
        {
            foreach (var format in IniAudioChecker.SupportedFormats)
            {
                var audioFile = Path.Combine(Directory, "preview" + format);
                if (File.Exists(audioFile))
                {
                    return new List<AudioChannel>()
                    {
                        new(SongStem.Preview, new FileStream(audioFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1))
                    };
                }
            }
            return LoadAudioStreams(SongStem.Crowd);
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

        private UnpackedIniEntry(string directory, ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile, in SongMetadata metadata)
            : base(metadata)
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
            if (parts == null)
            {
                return (result, null);
            }

            var abridged = new AbridgedFileInfo(chart.File);
            var metadata = SetMetadata(parts, HashWrapper.Hash(file), iniModifiers, defaultPlaylist);
            var entry = new UnpackedIniEntry(chartDirectory, chart.Type, abridged, iniFileInfo, metadata);
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

            var metadata = DeserializeMetadata(reader, strings);
            return new UnpackedIniEntry(directory, chart.Type, chartInfo, iniInfo, metadata);
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

            var metadata = DeserializeMetadata(reader, strings);
            return new UnpackedIniEntry(directory, chart.Type, chartInfo, iniInfo, metadata);
        }
    }
}