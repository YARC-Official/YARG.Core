using System.Collections.Generic;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song
{
#nullable enable
    public sealed class IniSubmetadata
    {
        public readonly ChartType chartType;
        public readonly AbridgedFileInfo chartFile;
        public readonly AbridgedFileInfo? iniFile;

        public IniSubmetadata(ChartType chartType, string chartFile, string? iniFile)
        {
            this.chartType = chartType;
            this.chartFile = new(chartFile);
            this.iniFile = iniFile != null ? new(iniFile) : null;
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

    public sealed partial class SongMetadata
    {
        public SongMetadata(IniSection section, IniSubmetadata iniData, AvailableParts parts, DrumType drumType)
        {
            // .ini songs are assumed to be masters and not covers
            _isMaster = true;
            _directory = Path.GetDirectoryName(iniData.chartFile.FullName);
            _parts = parts;
            _iniData = iniData;

            section.TryGet("name", out _name, DEFAULT_NAME);
            section.TryGet("artist", out _artist, DEFAULT_ARTIST);
            section.TryGet("album", out _album, DEFAULT_ALBUM);
            section.TryGet("genre", out _genre, DEFAULT_GENRE);
            section.TryGet("charter", out _charter, DEFAULT_CHARTER);
            section.TryGet("source", out _source, DEFAULT_SOURCE);
            section.TryGet("playlist", out _playlist, Path.GetFileName(Path.GetDirectoryName(_directory)));

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
                DrumType.FOUR_LANE or
                DrumType.FOUR_PRO => DrumsType.FourLane,
                DrumType.FIVE_LANE => DrumsType.FiveLane,
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

        private static readonly Dictionary<string, IniModifierCreator> MODIFIER_LIST = new()
        {
            { "Album",        new("album", ModifierNodeType.SORTSTRING_CHART ) },
            { "Artist",       new("artist", ModifierNodeType.SORTSTRING_CHART ) },
            { "Charter",      new("charter", ModifierNodeType.SORTSTRING_CHART ) },
            { "Difficulty",   new("diff_band", ModifierNodeType.INT32 ) },
            { "Genre",        new("genre", ModifierNodeType.SORTSTRING_CHART ) },
            { "Name",         new("name", ModifierNodeType.SORTSTRING_CHART ) },
            { "PreviewEnd",   new("previewEnd", ModifierNodeType.DOUBLE ) },
            { "PreviewStart", new("previewStart", ModifierNodeType.DOUBLE ) },
            { "Year",         new("year", ModifierNodeType.STRING_CHART ) },
            { "Offset",       new("offset", ModifierNodeType.DOUBLE ) },
        };

        public static (ScanResult, SongMetadata?) FromIni(YARGFile file, string chartFile, string? iniFile, ChartType type)
        {
            AvailableParts parts = new();
            IniSection modifiers;
            if (iniFile != null)
                modifiers = IniHandler.ReadSongIniFile(iniFile);
            else
                modifiers = new();

            DrumType drumType = default;
            if (type == ChartType.CHART)
                drumType = ParseChart(file, modifiers, parts);

            if (!modifiers.Contains("name"))
                return (ScanResult.NoName, null);

            if (type == ChartType.MID || type == ChartType.MIDI)
                drumType = ParseMidi(file, modifiers, parts);

            if (!parts.CheckScanValidity())
                return (ScanResult.NoNotes, null);

            IniSubmetadata metadata = new(type, chartFile, iniFile);
            parts.SetIntensities(modifiers);
            return (ScanResult.Success, new SongMetadata(modifiers, metadata, parts, drumType));
        }

        private static DrumType ParseChart(YARGFile file, IniSection modifiers, AvailableParts parts)
        {
            YARGChartFileReader reader = new(file);
            if (!reader.ValidateHeaderTrack())
                return DrumType.UNKNOWN;

            var chartMods = reader.ExtractModifiers(MODIFIER_LIST);
            modifiers.Append(chartMods);

            return parts.ParseChart(reader, GetDrumTypeFromModifier(modifiers));
        }

        private static DrumType ParseMidi(YARGFile file, IniSection modifiers, AvailableParts parts)
        {
            var drumType = GetDrumTypeFromModifier(modifiers);
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drumType == DrumType.UNKNOWN)
            {
                if (usePro)
                    drumType = DrumType.UNKNOWN_PRO;
            }
            else if (drumType == DrumType.FOUR_LANE && usePro)
                drumType = DrumType.FOUR_PRO;

            return parts.ParseMidi(file, drumType);
        }

        private static DrumType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumType.UNKNOWN;
            return fivelane ? DrumType.FIVE_LANE : DrumType.FOUR_LANE;
        }
    }
}
