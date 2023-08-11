using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Deserialization.Ini;
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

#nullable enable
        [Serializable]
        public sealed class IniSubmetadata
        {
            public static readonly (string, ChartType)[] CHART_FILE_TYPES =
            {
                new("notes.mid",   ChartType.Mid),
                new("notes.midi",  ChartType.Midi),
                new("notes.chart", ChartType.Chart),
            };

            public readonly ChartType chartType;
            public readonly AbridgedFileInfo chartFile;
            public readonly AbridgedFileInfo? iniFile;

            public IniSubmetadata(ChartType chartType, AbridgedFileInfo chartFile, AbridgedFileInfo? iniFile)
            {
                this.chartType = chartType;
                this.chartFile = chartFile;
                this.iniFile = iniFile;
            }

            public void Serialize(BinaryWriter writer)
            {
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

        private SongMetadata(IniSection section, IniSubmetadata iniData, AvailableParts parts, DrumPreparseType drumType, HashWrapper hash)
        {
            // .ini songs are assumed to be masters and not covers
            _isMaster = true;
            _directory = Path.GetDirectoryName(iniData.chartFile.FullName);
            _parts = parts;
            _hash = hash;
            _iniData = iniData;

            section.TryGet("name",     out _name,     DEFAULT_NAME);
            section.TryGet("artist",   out _artist,   DEFAULT_ARTIST);
            section.TryGet("album",    out _album,    DEFAULT_ALBUM);
            section.TryGet("genre",    out _genre,    DEFAULT_GENRE);
            section.TryGet("charter",  out _charter,  DEFAULT_CHARTER);
            section.TryGet("source",   out _source,   DEFAULT_SOURCE);
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
            section.TryGet("icon", out _icon);

            if (!section.TryGet("playlist_track", out _playlistTrack))
                _playlistTrack = -1;

            if (!section.TryGet("album_track", out _albumTrack))
                _albumTrack = -1;

            section.TryGet("song_length", out _songLength);

            if (!section.TryGet("preview", out _previewStart, out _previewEnd))
            {
                if (!section.TryGet("preview_start_time", out _previewStart) && section.TryGet("previewStart", out double previewValue))
                    _previewStart = (ulong)(1000 * previewValue);

                if (!section.TryGet("preview_end_time", out _previewEnd) && section.TryGet("previewStart", out previewValue))
                    _previewEnd = (ulong)(1000 * previewValue);
            }

            if (!section.TryGet("delay", out _chartOffset))
            {
                if (section.TryGet("offset", out double offset))
                    _chartOffset = (long)(1000 * offset);
            }

            section.TryGet("video_start_time", out _videoStartTime);
            if (!section.TryGet("video_end_time", out _videoEndTime))
                _videoEndTime = -1;

            var drumsType = drumType switch
            {
                DrumPreparseType.FourLane or
                DrumPreparseType.FourPro => DrumsType.FourLane,
                DrumPreparseType.FiveLane => DrumsType.FiveLane,
                _ => DrumsType.Unknown
            };

            if (!section.TryGet("hopo_frequency", out long hopoThreshold))
                hopoThreshold = -1;

            if (!section.TryGet("hopofreq", out int hopofreq_fof))
                hopofreq_fof = -1;

            section.TryGet("eighthnote_hopo", out bool eighthNoteHopo);

            if (!section.TryGet("hopofreq", out long susCutoffThreshold))
                susCutoffThreshold = -1;

            if (!section.TryGet("multiplier_note", out int starPowerNote))
                susCutoffThreshold = 116;

            _parseSettings = new()
            {
                DrumsType = drumsType,

                HopoThreshold = hopoThreshold,
                HopoFreq_FoF = hopofreq_fof,
                EighthNoteHopo = eighthNoteHopo,
                SustainCutoffThreshold = susCutoffThreshold,

                StarPowerNote = starPowerNote,
            };
        }

        private SongMetadata(IniSubmetadata iniData, YARGBinaryReader reader, CategoryCacheStrings strings) : this(reader, strings)
        {
            _iniData = iniData;
        }

        private static DrumPreparseType ParseChart(IYARGChartReader reader, IniSection modifiers, AvailableParts parts)
        {
            if (!reader.ValidateHeaderTrack())
                return DrumPreparseType.Unknown;

            var chartMods = reader.ExtractModifiers(CHART_MODIFIER_LIST);
            modifiers.Append(chartMods);

            return parts.ParseChart(reader, GetDrumTypeFromModifier(modifiers));
        }

        private static DrumPreparseType ParseMidi(byte[] file, IniSection modifiers, AvailableParts parts)
        {
            var drumType = GetDrumTypeFromModifier(modifiers);
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drumType == DrumPreparseType.Unknown)
            {
                if (usePro)
                    drumType = DrumPreparseType.UnknownPro;
            }
            else if (drumType == DrumPreparseType.FourLane && usePro)
                drumType = DrumPreparseType.FourPro;

            return parts.ParseMidi(file, drumType);
        }

        private static DrumPreparseType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumPreparseType.Unknown;
            return fivelane ? DrumPreparseType.FiveLane : DrumPreparseType.FourLane;
        }

        public static (ScanResult, SongMetadata?) FromIni(byte[] file, string chartFile, string? iniFile, int chartTypeIndex)
        {
            AvailableParts parts = new();
            AbridgedFileInfo? iniInfo;
            IniSection modifiers;
            if (iniFile != null)
            {
                modifiers = IniHandler.ReadSongIniFile(iniFile);
                iniInfo = new AbridgedFileInfo(iniFile);
            }
            else
            {
                modifiers = new();
                iniInfo = null;
            }

            var chartType = IniSubmetadata.CHART_FILE_TYPES[chartTypeIndex].Item2;
            DrumPreparseType drumType = default;
            if (chartType == ChartType.Chart)
            {
                try
                {
                    drumType = ParseChart(new YARGChartFileReader(file), modifiers, parts);
                }
                catch (BadEncodingException)
                {
                    YargTrace.LogInfo("UTF-8 preferred for .chart encoding");
                    drumType = ParseChart(new YARGChartFileReader_Char(chartFile), modifiers, parts);
                }
                catch(Exception ex)
                {
                    YargTrace.LogException(ex, ex.Message);
                    return (ScanResult.InvalidDotChartEncoding, null);
                }
            }

            if (!modifiers.Contains("name"))
                return (ScanResult.NoName, null);

            if (chartType == ChartType.Mid || chartType == ChartType.Midi)
                drumType = ParseMidi(file, modifiers, parts);

            if (!parts.CheckScanValidity())
                return (ScanResult.NoNotes, null);

            IniSubmetadata metadata = new(chartType, new AbridgedFileInfo(chartFile), iniInfo);
            parts.SetIntensities(modifiers);
            return (ScanResult.Success, new SongMetadata(modifiers, metadata, parts, drumType, HashWrapper.Create(file)));
        }

        public static SongMetadata? IniFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IniSubmetadata.CHART_FILE_TYPES.Length)
                return null;

            ref var chartType = ref IniSubmetadata.CHART_FILE_TYPES[chartTypeIndex];
            FileInfo chartFile = new(Path.Combine(directory, chartType.Item1));
            if (!chartFile.Exists)
                return null;

            if (chartFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;

            FileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = new(Path.Combine(directory, "song.ini"));
                if (!iniFile.Exists)
                    return null;

                if (iniFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return null;
            }

            IniSubmetadata iniData = new(chartType.Item2, chartFile, iniFile);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        public static SongMetadata? IniFromCache_Quick(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IniSubmetadata.CHART_FILE_TYPES.Length)
                return null;

            ref var chartType = ref IniSubmetadata.CHART_FILE_TYPES[chartTypeIndex];
            var lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo chartFile = new(Path.Combine(directory, chartType.Item1), lastWrite);
            AbridgedFileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                lastWrite = DateTime.FromBinary(reader.ReadInt64());
                iniFile = new(Path.Combine(directory, "song.ini"), lastWrite);
            }

            IniSubmetadata iniData = new(chartType.Item2, chartFile, iniFile);
            return new SongMetadata(iniData, reader, strings)
            {
                _directory = directory
            };
        }

        private SongChart LoadIniChart()
        {
            string notesFile = IniData!.chartFile.FullName;
            YargTrace.LogInfo($"Loading chart file {notesFile}");
            return SongChart.FromFile(_parseSettings, notesFile);
        }
    }
}
