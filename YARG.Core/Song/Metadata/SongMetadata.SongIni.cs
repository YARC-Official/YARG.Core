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

    public sealed partial class SongMetadata
    {
        private static readonly Dictionary<string, IniModifierCreator> MODIFIER_LIST = new()
        {
            { "Album",        new("album", ModifierCreatorType.SortString_Chart ) },
            { "Artist",       new("artist", ModifierCreatorType.SortString_Chart ) },
            { "Charter",      new("charter", ModifierCreatorType.SortString_Chart ) },
            { "Difficulty",   new("diff_band", ModifierCreatorType.Int32 ) },
            { "Genre",        new("genre", ModifierCreatorType.SortString_Chart ) },
            { "Name",         new("name", ModifierCreatorType.SortString_Chart ) },
            { "PreviewEnd",   new("previewEnd", ModifierCreatorType.Double ) },
            { "PreviewStart", new("previewStart", ModifierCreatorType.Double ) },
            { "Year",         new("year", ModifierCreatorType.String_Chart ) },
            { "Offset",       new("offset", ModifierCreatorType.Double ) },
        };

        private SongMetadata(IniSection section, IniSubmetadata iniData, AvailableParts parts, DrumType drumType)
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
                DrumType.FourLane or
                DrumType.FourPro => DrumsType.FourLane,
                DrumType.FiveLane => DrumsType.FiveLane,
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

        private static DrumType ParseChart(byte[] file, IniSection modifiers, AvailableParts parts)
        {
            YARGChartFileReader reader = new(file);
            if (!reader.ValidateHeaderTrack())
                return DrumType.Unknown;

            var chartMods = reader.ExtractModifiers(MODIFIER_LIST);
            modifiers.Append(chartMods);

            return parts.ParseChart(reader, GetDrumTypeFromModifier(modifiers));
        }

        private static DrumType ParseMidi(byte[] file, IniSection modifiers, AvailableParts parts)
        {
            var drumType = GetDrumTypeFromModifier(modifiers);
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drumType == DrumType.Unknown)
            {
                if (usePro)
                    drumType = DrumType.UnknownPro;
            }
            else if (drumType == DrumType.FourLane && usePro)
                drumType = DrumType.FourPro;

            return parts.ParseMidi(file, drumType);
        }

        private static DrumType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumType.Unknown;
            return fivelane ? DrumType.FiveLane : DrumType.FourLane;
        }

        public static (ScanResult, SongMetadata?) FromIni(byte[] file, string chartFile, string? iniFile, ChartType type)
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

            DrumType drumType = default;
            if (type == ChartType.CHART)
                drumType = ParseChart(file, modifiers, parts);

            if (!modifiers.Contains("name"))
                return (ScanResult.NoName, null);

            if (type == ChartType.MID || type == ChartType.MIDI)
                drumType = ParseMidi(file, modifiers, parts);

            if (!parts.CheckScanValidity())
                return (ScanResult.NoNotes, null);

            IniSubmetadata metadata = new(type, new AbridgedFileInfo(chartFile), iniInfo);
            parts.SetIntensities(modifiers);
            return (ScanResult.Success, new SongMetadata(modifiers, metadata, parts, drumType));
        }
    }
}
