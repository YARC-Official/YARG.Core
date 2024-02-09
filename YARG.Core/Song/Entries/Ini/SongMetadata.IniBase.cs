﻿using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Preparsers;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class IniChartNode<T>
    {
        public readonly ChartType Type;
        public readonly T File;

        public IniChartNode(ChartType type, T file)
        {
            Type = type;
            File = file;
        }
    }

    public abstract class IniSubEntry : SongEntry
    {
        public static class IniAudioChecker
        {
            public static readonly string[] SupportedStems = { "song", "guitar", "bass", "rhythm", "keys", "vocals", "vocals_1", "vocals_2", "drums", "drums_1", "drums_2", "drums_3", "drums_4", "crowd", };
            public static readonly string[] SupportedFormats = { ".opus", ".ogg", ".mp3", ".wav", ".aiff", };
            private static readonly HashSet<string> SupportedAudioFiles = new();
            static IniAudioChecker()
            {
                foreach (string stem in SupportedStems)
                    foreach (string format in SupportedFormats)
                        SupportedAudioFiles.Add(stem + format);
            }

            public static bool IsAudioFile(string file)
            {
                return SupportedAudioFiles.Contains(file);
            }
        }

        public static readonly IniChartNode<string>[] CHART_FILE_TYPES =
        {
            new(ChartType.Mid, "notes.mid"),
            new(ChartType.Midi, "notes.midi"),
            new(ChartType.Chart, "notes.chart"),
        };

        protected static readonly Dictionary<string, IniModifierCreator> CHART_MODIFIER_LIST = new()
        {
            { "Album",        new("album",        ModifierCreatorType.SortString_Chart ) },
            { "Artist",       new("artist",       ModifierCreatorType.SortString_Chart ) },
            { "Charter",      new("charter",      ModifierCreatorType.SortString_Chart ) },
            { "Difficulty",   new("diff_band",    ModifierCreatorType.Int32 ) },
            { "Genre",        new("genre",        ModifierCreatorType.SortString_Chart ) },
            { "Name",         new("name",         ModifierCreatorType.SortString_Chart ) },
            { "PreviewEnd",   new("previewEnd",   ModifierCreatorType.Double ) },
            { "PreviewStart", new("previewStart", ModifierCreatorType.Double ) },
            { "Year",         new("year_chart",   ModifierCreatorType.String_Chart ) },
            { "Offset",       new("offset",       ModifierCreatorType.Double ) },
        };

        protected static readonly string[] ALBUMART_FILES =
        {
            "album.png", "album.jpg", "album.jpeg",
        };

        public abstract ChartType Type { get; }

        protected abstract void Serialize(BinaryWriter writer, string groupDirectory);
        protected abstract Stream? GetChartStream();

        public byte[] Serialize(CategoryCacheWriteNode node, string groupDirectory)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            Metadata.Serialize(writer, node);
            Serialize(writer, groupDirectory);

            return ms.ToArray();
        }

        public override SongChart? LoadChart()
        {
            using var stream = GetChartStream();
            if (stream == null)
                return null;

            if (Type != ChartType.Chart)
                return SongChart.FromMidi(ParseSettings, MidFileLoader.LoadMidiFile(stream));

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(ParseSettings, reader.ReadToEnd());
        }

        public override byte[]? LoadMiloData()
        {
            return null;
        }

        protected static (ScanResult Result, AvailableParts? Parts) ScanIniChartFile(byte[] file, ChartType chartType, IniSection modifiers)
        {
            AvailableParts parts = new();
            DrumPreparseHandler drums = new()
            {
                Type = GetDrumTypeFromModifier(modifiers)
            };

            if (chartType == ChartType.Chart)
            {
                var byteReader = YARGTextLoader.TryLoadByteText(file);
                if (byteReader != null)
                    ParseDotChart<byte, ByteStringDecoder, DotChartByte>(byteReader, modifiers, parts, drums);
                else
                {
                    var charReader = YARGTextLoader.LoadCharText(file);
                    ParseDotChart<char, CharStringDecoder, DotChartChar>(charReader, modifiers, parts, drums);
                }
            }
            else // if (chartType == ChartType.Mid || chartType == ChartType.Midi) // Uncomment for any future file type
            {
                if (!ParseDotMidi(file, modifiers, parts, drums))
                {
                    return (ScanResult.MultipleMidiTrackNames, null);
                }
            }

            parts.SetDrums(drums);

            if (!parts.CheckScanValidity())
                return (ScanResult.NoNotes, null);

            if (!modifiers.Contains("name"))
                return (ScanResult.NoName, null);

            parts.SetIntensities(modifiers);
            return (ScanResult.Success, parts);
        }

        private static void ParseDotChart<TChar, TDecoder, TBase>(YARGTextReader<TChar, TDecoder> textReader, IniSection modifiers, AvailableParts parts, DrumPreparseHandler drums)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            YARGChartFileReader<TChar, TDecoder, TBase> chartReader = new(textReader);
            if (chartReader.ValidateHeaderTrack())
            {
                var chartMods = chartReader.ExtractModifiers(CHART_MODIFIER_LIST);
                modifiers.Append(chartMods);
            }
            parts.ParseChart(chartReader, drums);

            if (drums.Type == DrumsType.Unknown && drums.ValidatedDiffs > 0)
                drums.Type = DrumsType.FourLane;
        }

        private static bool ParseDotMidi(byte[] file, IniSection modifiers, AvailableParts parts, DrumPreparseHandler drums)
        {
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drums.Type == DrumsType.Unknown)
            {
                if (usePro)
                    drums.Type = DrumsType.UnknownPro;
            }
            else if (drums.Type == DrumsType.FourLane && usePro)
                drums.Type = DrumsType.ProDrums;

            return parts.ParseMidi(file, drums);
        }

        private static DrumsType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumsType.Unknown;
            return fivelane ? DrumsType.FiveLane : DrumsType.FourLane;
        }
    }

    public partial struct SongMetadata
    {
        public SongMetadata(AvailableParts parts, HashWrapper hash, IniSection section, string defaultPlaylist)
        {
            Parts = parts;
            Hash = hash;
            ParseSettings = ParseSettings.Default;
            ParseSettings.DrumsType = parts.GetDrumType();

            section.TryGet("name", out Name, DEFAULT_NAME);
            section.TryGet("artist", out Artist, DEFAULT_ARTIST);
            section.TryGet("album", out Album, DEFAULT_ALBUM);
            section.TryGet("genre", out Genre, DEFAULT_GENRE);

            if (!section.TryGet("charter", out Charter, DEFAULT_CHARTER))
            {
                section.TryGet("frets", out Charter, DEFAULT_CHARTER);
            }

            section.TryGet("icon", out Source, DEFAULT_SOURCE);
            section.TryGet("playlist", out Playlist, defaultPlaylist);

            _unmodifiedYear = DEFAULT_YEAR;
            _parsedYear = DEFAULT_YEAR;
            _intYear = int.MaxValue;

            _songLength = 0;
            _songOffset = 0;

            _previewStart = 0;
            _previewEnd = 0;

            _videoStartTime = 0;
            _videoEndTime = -1;

            section.TryGet("loading_phrase", out LoadingPhrase);

            if (!section.TryGet("playlist_track", out PlaylistTrack))
                PlaylistTrack = -1;

            if (!section.TryGet("album_track", out AlbumTrack))
                AlbumTrack = -1;

            section.TryGet("song_length", out _songLength);

            section.TryGet("video_start_time", out _videoStartTime);
            _videoEndTime = section.TryGet("video_end_time", out long videoEndTime) ? videoEndTime : -1000;

            if (!section.TryGet("hopo_frequency", out ParseSettings.HopoThreshold))
                ParseSettings.HopoThreshold = -1;

            if (!section.TryGet("hopofreq", out ParseSettings.HopoFreq_FoF))
                ParseSettings.HopoFreq_FoF = -1;

            section.TryGet("eighthnote_hopo", out ParseSettings.EighthNoteHopo);

            if (!section.TryGet("sustain_cutoff_threshold", out ParseSettings.SustainCutoffThreshold))
                ParseSettings.SustainCutoffThreshold = -1;

            if (!section.TryGet("multiplier_note", out ParseSettings.StarPowerNote))
                ParseSettings.StarPowerNote = -1;

            IsMaster = !section.TryGet("tags", out string tag) || tag.ToLower() != "cover";

            if (section.TryGet("year", out _unmodifiedYear))
            {
                Year = _unmodifiedYear;
            }
            else if (section.TryGet("year_chart", out _unmodifiedYear))
            {
                if (_unmodifiedYear.StartsWith(", "))
                    Year = _unmodifiedYear[2..];
                else if (_unmodifiedYear.StartsWith(','))
                    Year = _unmodifiedYear[1..];
                else
                    Year = _unmodifiedYear;
            }
            else
                _unmodifiedYear = DEFAULT_YEAR;

            if (!section.TryGet("preview", out _previewStart, out _previewEnd))
            {
                if (!section.TryGet("preview_start_time", out _previewStart) &&
                    section.TryGet("previewStart", out double previewStartSeconds))
                    PreviewStartSeconds = previewStartSeconds;

                if (!section.TryGet("preview_end_time", out _previewEnd) &&
                    section.TryGet("previewEnd", out double previewEndSeconds))
                    PreviewEndSeconds = previewEndSeconds;
            }


            if (!section.TryGet("delay", out _songOffset) || _songOffset == 0)
            {
                if (section.TryGet("offset", out double songOffsetSeconds))
                {
                    SongOffsetSeconds = songOffsetSeconds;
                }
            }
        }
    }
}
