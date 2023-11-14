using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public sealed partial class SongMetadata
    {
        /// <summary>
        /// The type of chart file to read.
        /// </summary>
        public enum ChartType
        {
            Mid,
            Midi,
            Chart,
        };

        public class IniChartNode
        {
            public readonly ChartType Type;
            public readonly string File;

            public IniChartNode(ChartType type, string file)
            {
                Type = type;
                File = file;
            }
        }

        private static readonly Dictionary<string, IniModifierCreator> CHART_MODIFIER_LIST = new()
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

        public interface IIniMetadata
        {
            public static readonly IniChartNode[] CHART_FILE_TYPES =
            {
                new(ChartType.Mid, "notes.mid"),
                new(ChartType.Midi, "notes.midi"),
                new(ChartType.Chart, "notes.chart"),
            };

            public string Root { get; }
            public ChartType Type { get; }

            public void Serialize(BinaryWriter writer, string groupDirectory);
            public Stream? GetChartStream();
        }

        public static class IniAudioChecker
        {
            public  static readonly string[] SupportedStems = { "song", "guitar", "bass", "rhythm", "keys", "vocals", "vocals_1", "vocals_2", "drums", "drums_1", "drums_2", "drums_3", "drums_4", "crowd", };
            public  static readonly string[] SupportedFormats = { ".opus", ".ogg", ".mp3", ".wav", ".aiff", };
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

        [Serializable]
        public sealed class IniSubmetadata : IIniMetadata
        {
            private readonly string directory;
            private readonly ChartType chartType;
            private readonly AbridgedFileInfo chartFile;
            private readonly AbridgedFileInfo? iniFile;

            public string Root => directory;
            public ChartType Type => chartType;

            public IniSubmetadata(string directory, ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile)
            {
                this.directory = directory;
                this.chartType = chartType;
                this.chartFile = chartFile;
                this.iniFile = iniFile;
            }

            public void Serialize(BinaryWriter writer, string groupDirectory)
            {
                string relative = Path.GetRelativePath(groupDirectory, directory);
                if (relative == ".")
                    relative = string.Empty;

                writer.Write(false);
                writer.Write(relative);
                writer.Write((byte) chartType);
                writer.Write(chartFile.LastWriteTime.ToBinary());
                if (iniFile != null)
                {
                    writer.Write(true);
                    writer.Write(iniFile.LastWriteTime.ToBinary());
                }
                else
                    writer.Write(false);
            }

            public Stream? GetChartStream()
            {
                if (!chartFile.IsStillValid())
                    return null;

                if (iniFile == null)
                {
                    if (File.Exists(Path.Combine(directory, "song.ini")))
                        return null;
                }
                else if (!iniFile.IsStillValid())
                    return null;

                return new FileStream(chartFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            }

            public static bool DoesSoloChartHaveAudio(string directory)
            {
                foreach (string subFile in System.IO.Directory.EnumerateFileSystemEntries(directory))
                    if (IniAudioChecker.IsAudioFile(Path.GetFileName(subFile)))
                        return true;
                return false;
            }
        }

        private SongMetadata(IIniMetadata iniData, AvailableParts parts, HashWrapper hash, IniSection modifiers)
        {
            // .ini songs are assumed to be masters and not covers
            _isMaster = true;
            _directory = iniData.Root;
            _parts = parts;
            _hash = hash;
            _iniData = iniData;

            _parseSettings.DrumsType = parts.GetDrumType();
            SetIniModifierData(modifiers);
        }

        private SongMetadata(IIniMetadata iniData, YARGBinaryReader reader, CategoryCacheStrings strings) : this(reader, strings)
        {
            _iniData = iniData;
        }

        private void SetIniModifierData(IniSection section)
        {
            section.TryGet("name", out _name, DEFAULT_NAME);
            section.TryGet("artist", out _artist, DEFAULT_ARTIST);
            section.TryGet("album", out _album, DEFAULT_ALBUM);
            section.TryGet("genre", out _genre, DEFAULT_GENRE);
            if (!section.TryGet("charter", out _charter, DEFAULT_CHARTER))
                section.TryGet("frets", out _charter, DEFAULT_CHARTER);
            section.TryGet("icon", out _source, DEFAULT_SOURCE);
            section.TryGet("playlist", out _playlist, Path.GetFileName(Path.GetDirectoryName(_directory)));

            if (section.TryGet("year", out _unmodifiedYear))
                Year = _unmodifiedYear;
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


            section.TryGet("loading_phrase", out _loadingPhrase);

            if (!section.TryGet("playlist_track", out _playlistTrack))
                _playlistTrack = -1;

            if (!section.TryGet("album_track", out _albumTrack))
                _albumTrack = -1;

            section.TryGet("song_length", out _songLength);

            if (!section.TryGet("preview", out _previewStart, out _previewEnd))
            {
                if (!section.TryGet("preview_start_time", out _previewStart) &&
                    section.TryGet("previewStart", out double previewStartSeconds))
                    PreviewStartSeconds = previewStartSeconds;

                if (!section.TryGet("preview_end_time", out _previewEnd) &&
                    section.TryGet("previewEnd", out double previewEndSeconds))
                    PreviewEndSeconds = previewEndSeconds;
            }


            if (!section.TryGet("delay", out _songOffset) &&
                section.TryGet("offset", out double songOffsetSeconds))
                SongOffsetSeconds = songOffsetSeconds;


            section.TryGet("video_start_time", out _videoStartTime);
            _videoEndTime = section.TryGet("video_end_time", out long videoEndTime) ? videoEndTime : -1000;



            if (!section.TryGet("hopo_frequency", out _parseSettings.HopoThreshold))
                _parseSettings.HopoThreshold = -1;

            if (!section.TryGet("hopofreq", out _parseSettings.HopoFreq_FoF))
                _parseSettings.HopoFreq_FoF = -1;

            section.TryGet("eighthnote_hopo", out _parseSettings.EighthNoteHopo);

            if (!section.TryGet("sustain_cutoff_threshold", out _parseSettings.SustainCutoffThreshold))
                _parseSettings.SustainCutoffThreshold = -1;

            if (!section.TryGet("multiplier_note", out _parseSettings.StarPowerNote))
                _parseSettings.StarPowerNote = -1;
        }

        public static (ScanResult, SongMetadata?) FromIni(string directory, IniChartNode chart, string? iniFile)
        {
            IniSection iniModifiers;
            AbridgedFileInfo? iniFileInfo = null;
            if (iniFile != null)
            {
                iniModifiers = SongIniHandler.ReadSongIniFile(iniFile);
                iniFileInfo = new AbridgedFileInfo(iniFile);
            }
            else if (IniSubmetadata.DoesSoloChartHaveAudio(Path.GetDirectoryName(chart.File)))
                iniModifiers = new();
            else
                return (ScanResult.LooseChart_NoAudio, null);

            IniSubmetadata metadata = new(directory, chart.Type, new AbridgedFileInfo(chart.File), iniFileInfo);

            byte[] file = File.ReadAllBytes(chart.File);
            var result = ScanIniChartFile(file, chart.Type, iniModifiers);
            return (result.Item1, result.Item2 != null ? new(metadata, result.Item2, HashWrapper.Create(file), iniModifiers) : null);
        }

        public static SongMetadata? IniFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var chartInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, chart.File), reader);
            if (chartInfo == null)
                return null;

            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                iniInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "song.ini"), reader);
                if (iniInfo == null)
                    return null;
            }
            else if (!IniSubmetadata.DoesSoloChartHaveAudio(directory))
                return null;

            IniSubmetadata iniData = new(baseDirectory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        public static SongMetadata? IniFromCache_Quick(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IIniMetadata.CHART_FILE_TYPES.Length)
                return null;

            var chart = IIniMetadata.CHART_FILE_TYPES[chartTypeIndex];
            var lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo chartInfo = new(Path.Combine(directory, chart.File), lastWrite);
            AbridgedFileInfo? iniInfo = null;
            if (reader.ReadBoolean())
            {
                lastWrite = DateTime.FromBinary(reader.ReadInt64());
                iniInfo = new(Path.Combine(directory, "song.ini"), lastWrite);
            }

            IniSubmetadata iniData = new(baseDirectory, chart.Type, chartInfo, iniInfo);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        private static (ScanResult, AvailableParts?) ScanIniChartFile(byte[] file, ChartType chartType, IniSection modifiers)
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
                ParseDotMidi(file, modifiers, parts, drums);

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

        private static void ParseDotMidi(byte[] file, IniSection modifiers, AvailableParts parts, DrumPreparseHandler drums)
        {
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drums.Type == DrumsType.Unknown)
            {
                if (usePro)
                    drums.Type = DrumsType.UnknownPro;
            }
            else if (drums.Type == DrumsType.FourLane && usePro)
                drums.Type = DrumsType.ProDrums;

            parts.ParseMidi(file, drums);
        }

        private static DrumsType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumsType.Unknown;
            return fivelane ? DrumsType.FiveLane : DrumsType.FourLane;
        }
    }
}
