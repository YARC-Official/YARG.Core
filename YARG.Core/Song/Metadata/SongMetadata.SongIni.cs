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

        public class IniChartNode<TFileType>
        {
            public readonly ChartType Type;
            public readonly TFileType File;

            public IniChartNode(ChartType type, TFileType file)
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

            public bool Validate(string directory)
            {
                if (!chartFile.IsStillValid())
                    return false;

                if (iniFile == null)
                    return !File.Exists(Path.Combine(directory, "song.ini"));

                return iniFile.IsStillValid();
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

        private SongMetadata(IniSection section, IniSubmetadata iniData, AvailableParts parts, DrumsType drumType, HashWrapper hash)
        {
            // .ini songs are assumed to be masters and not covers
            _isMaster = true;
            _directory = Path.GetDirectoryName(iniData.chartFile.FullName);
            _parts = parts;
            _hash = hash;
            _iniData = iniData;

            _parseSettings.DrumsType = drumType switch
            {
                DrumsType.ProDrums => DrumsType.FourLane,
                // Only possible if 1. is .mid & 2. does not have drums
                DrumsType.UnknownPro => DrumsType.Unknown,
                _ => drumType
            };

            SetIniModifierData(section);
        }

        private SongMetadata(IniSubmetadata iniData, YARGBinaryReader reader, CategoryCacheStrings strings) : this(reader, strings)
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

        public static (ScanResult, SongMetadata?) FromIni(IniChartNode<string> chart, string? iniFile)
        {
            var iniModifiers = LoadIniFile(chart.File, iniFile);
            if (iniModifiers.Item1 == null)
                return (ScanResult.LooseChart_NoAudio, null);
            
            byte[] file = File.ReadAllBytes(chart.File);
            var result = Scan(file, chart.Type, iniModifiers.Item1);

            if (!result.Item1.CheckScanValidity())
                return (ScanResult.NoNotes, null);

            if (!iniModifiers.Item1.Contains("name"))
                return (ScanResult.NoName, null);

            IniSubmetadata metadata = new(chart.Type, new AbridgedFileInfo(chart.File), iniModifiers.Item2);
            result.Item1.SetIntensities(iniModifiers.Item1);
            return (ScanResult.Success, new SongMetadata(iniModifiers.Item1, metadata, result.Item1, result.Item2, HashWrapper.Create(file)));
        }

        public static SongMetadata? IniFromCache(string baseDirectory, YARGBinaryReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= IniSubmetadata.CHART_FILE_TYPES.Length)
                return null;

            ref var chartType = ref IniSubmetadata.CHART_FILE_TYPES[chartTypeIndex];
            var chartFile = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, chartType.Item1), reader);
            if (chartFile == null)
                return null;

            AbridgedFileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, "song.ini"), reader);
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

        private static (IniSection?, AbridgedFileInfo?) LoadIniFile(string chartFile, string? iniFile)
        {
            IniSection? modifiers = null;
            AbridgedFileInfo? iniInfo = null;
            if (iniFile != null)
            {
                modifiers = SongIniHandler.ReadSongIniFile(iniFile);
                iniInfo = new AbridgedFileInfo(iniFile);
            }
            else if (IniSubmetadata.DoesSoloChartHaveAudio(Path.GetDirectoryName(chartFile)))
                modifiers = new();
            return (modifiers, iniInfo);
        }

        private static (AvailableParts, DrumsType) Scan(byte[] file, ChartType chartType, IniSection modifiers)
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
                    ParseChart<byte, ByteStringDecoder, DotChartByte>(byteReader, modifiers, parts, drums);
                else
                {
                    var charReader = YARGTextLoader.LoadCharText(file);
                    ParseChart<char, CharStringDecoder, DotChartChar>(charReader, modifiers, parts, drums);
                }
            }
            else // if (chartType == ChartType.Mid || chartType == ChartType.Midi) // Uncomment for any future file type
                ParseMidi(file, modifiers, parts, drums);

            parts.SetDrums(drums);
            return (parts, drums.Type);
        }

         private static void ParseChart<TChar, TDecoder, TBase>(YARGTextReader<TChar, TDecoder> textReader, IniSection modifiers, AvailableParts parts, DrumPreparseHandler drums)
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

        private static void ParseMidi(byte[] file, IniSection modifiers, AvailableParts parts, DrumPreparseHandler drums)
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
