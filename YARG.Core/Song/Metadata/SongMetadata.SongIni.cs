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

            private static readonly string[] SupportedFormats =
            {
                ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
            };

            private static readonly string[] SupportedStems =
            {
                "song",
                "guitar",
                "bass",
                "rhythm",
                "keys",
                "vocals",
                "vocals_1",
                "vocals_2",
                "drums",
                "drums_1",
                "drums_2",
                "drums_3",
                "drums_4",
                "crowd",
            };

            public static bool DoesSoloChartHaveAudio(string directory)
            {
                foreach (string subFile in System.IO.Directory.EnumerateFileSystemEntries(directory))
                {
                    string ext = Path.GetExtension(subFile);
                    for (int i = 0; i < SupportedFormats.Length; ++i)
                    {
                        if (SupportedFormats[i] == ext)
                        {
                            string stem = Path.GetFileName(subFile);
                            for (int j = 0; j < SupportedStems.Length; ++j)
                            {
                                if (SupportedStems[j] == stem)
                                    return true;
                            }
                            break;
                        }
                    }
                }
                return false;
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

        private SongMetadata(IniSection section, IniSubmetadata iniData, AvailableParts parts, DrumsType drumType, HashWrapper hash)
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
            if (!section.TryGet("charter", out _charter, DEFAULT_CHARTER))
                section.TryGet("frets",    out _charter, DEFAULT_CHARTER);
            section.TryGet("icon",     out _source,   DEFAULT_SOURCE);
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

            if (_parseSettings.DrumsType == DrumsType.ProDrums)
                _parseSettings.DrumsType = DrumsType.FourLane;
            else if (_parseSettings.DrumsType == DrumsType.UnknownPro)
                _parseSettings.DrumsType = DrumsType.Unknown;

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

        private SongMetadata(IniSubmetadata iniData, YARGBinaryReader reader, CategoryCacheStrings strings) : this(reader, strings)
        {
            _iniData = iniData;
        }

        private static DrumsType ParseChart<TChar, TBase, TDecoder>(YARGTextReader<TChar> textReader, IniSection modifiers, AvailableParts parts)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TBase : unmanaged, IDotChartBases<TChar>
            where TDecoder : StringDecoder<TChar>, new()
        {
            TDecoder decoder = new();
            YARGChartFileReader<TChar, TBase> chartReader = new(textReader);
            if (!chartReader.ValidateHeaderTrack())
                return DrumsType.Unknown;

            var chartMods = chartReader.ExtractModifiers(decoder, CHART_MODIFIER_LIST);
            modifiers.Append(chartMods);

            return parts.ParseChart(chartReader, GetDrumTypeFromModifier(modifiers));
        }

        private static DrumsType ParseMidi(byte[] file, IniSection modifiers, AvailableParts parts)
        {
            var drumType = GetDrumTypeFromModifier(modifiers);
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drumType == DrumsType.Unknown)
            {
                if (usePro)
                    drumType = DrumsType.UnknownPro;
            }
            else if (drumType == DrumsType.FourLane && usePro)
                drumType = DrumsType.ProDrums;

            return parts.ParseMidi(file, drumType);
        }

        private static DrumsType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumsType.Unknown;
            return fivelane ? DrumsType.FiveLane : DrumsType.FourLane;
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
            else if (IniSubmetadata.DoesSoloChartHaveAudio(Path.GetDirectoryName(chartFile)))
            {
                modifiers = new();
                iniInfo = null;
            }
            else
                return (ScanResult.LooseChart_NoAudio, null);

            var chartType = IniSubmetadata.CHART_FILE_TYPES[chartTypeIndex].Item2;
            DrumsType drumType = default;
            if (chartType == ChartType.Chart)
            {
                var byteReader = YARGTextReader.TryLoadByteReader(file);
                if (byteReader != null)
                    drumType = ParseChart<byte, DotChartByte, ByteStringDecoder>(byteReader, modifiers, parts);
                else
                {
                    var charReader = YARGTextReader.LoadCharReader(file);
                    drumType = ParseChart<char, DotChartChar, CharStringDecoder>(charReader, modifiers, parts);
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
            var chartFile = ParseFileInfo(Path.Combine(directory, chartType.Item1), reader);
            if (chartFile == null)
                return null;

            AbridgedFileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = ParseFileInfo(Path.Combine(directory, "song.ini"), reader);
                if (iniFile == null)
                    return null;
            }
            else if (!IniSubmetadata.DoesSoloChartHaveAudio(directory))
                return null;

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
