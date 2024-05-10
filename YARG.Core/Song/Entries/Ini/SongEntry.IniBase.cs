using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Preparsers;

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

    public static class IniAudio
    {
        public static readonly string[] SupportedStems = { "song", "guitar", "bass", "rhythm", "keys", "vocals", "vocals_1", "vocals_2", "drums", "drums_1", "drums_2", "drums_3", "drums_4", "crowd", };
        public static readonly string[] SupportedFormats = { ".opus", ".ogg", ".mp3", ".wav", ".aiff", };
        private static readonly HashSet<string> SupportedAudioFiles = new();

        static IniAudio()
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

    public abstract class IniSubEntry : SongEntry
    {
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

        protected static readonly string[] ALBUMART_FILES;
        protected static readonly string[] PREVIEW_FILES;

        static IniSubEntry()
        {
            ALBUMART_FILES = new string[IMAGE_EXTENSIONS.Length];
            for (int i = 0; i < ALBUMART_FILES.Length; i++)
            {
                ALBUMART_FILES[i] = "album" + IMAGE_EXTENSIONS[i];
            }

            PREVIEW_FILES = new string[IniAudio.SupportedFormats.Length];
            for (int i = 0; i < PREVIEW_FILES.Length; i++ )
            {
                PREVIEW_FILES[i] = "preview" + IniAudio.SupportedFormats[i];
            }
        }

        protected readonly string _background;
        protected readonly string _video;
        protected readonly string _cover;
        public readonly bool Video_Loop;

        public abstract ChartType Type { get; }

        protected IniSubEntry(in AvailableParts parts, in HashWrapper hash, IniSection modifiers, string defaultPlaylist)
            : base(in parts, in hash, modifiers, defaultPlaylist)
        {
            if (modifiers.TryGet("background", out _background))
            {
                string ext = Path.GetExtension(_background.Trim('\"')).ToLower();
                _background = IMAGE_EXTENSIONS.Contains(ext) ? _background.ToLowerInvariant() : string.Empty;
            }

            if (modifiers.TryGet("video", out _video))
            {
                string ext = Path.GetExtension(_video.Trim('\"')).ToLower();
                _video = VIDEO_EXTENSIONS.Contains(ext) ? _video.ToLowerInvariant() : string.Empty;
            }

            if (modifiers.TryGet("cover", out _cover))
            {
                string ext = Path.GetExtension(_cover.Trim('\"')).ToLower();
                _cover = IMAGE_EXTENSIONS.Contains(ext) ? _cover.ToLowerInvariant() : string.Empty;
            }
            modifiers.TryGet("video_loop", out Video_Loop);
        }

        protected IniSubEntry(BinaryReader reader, CategoryCacheStrings strings)
            : base(reader, strings)
        {
            _background = reader.ReadString();
            _video = reader.ReadString();
            _cover = reader.ReadString();
            Video_Loop = reader.ReadBoolean();
        }

        protected abstract Stream? GetChartStream();

        protected abstract void SerializeSubData(BinaryWriter writer);

        public byte[] Serialize(CategoryCacheWriteNode node, string groupDirectory)
        {
            string relativePath = Path.GetRelativePath(groupDirectory, Directory);
            if (relativePath == ".")
                relativePath = string.Empty;

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(SubType == EntryType.Sng);
            writer.Write(relativePath);

            SerializeSubData(writer);
            SerializeMetadata(writer, node);

            writer.Write(_background);
            writer.Write(_video);
            writer.Write(_cover);
            writer.Write(Video_Loop);
            return ms.ToArray();
        }

        public override SongChart? LoadChart()
        {
            using var stream = GetChartStream();
            if (stream == null)
                return null;

            if (Type != ChartType.Chart)
            {
                return SongChart.FromMidi(_parseSettings, MidFileLoader.LoadMidiFile(stream));
            }

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(_parseSettings, reader.ReadToEnd());
        }

        public override byte[]? LoadMiloData()
        {
            return null;
        }

        protected static (ScanResult Result, AvailableParts Parts) ScanIniChartFile(byte[] file, ChartType chartType, IniSection modifiers)
        {
            DrumPreparseHandler drums = new()
            {
                Type = GetDrumTypeFromModifier(modifiers)
            };

            var parts = AvailableParts.Default;
            if (chartType == ChartType.Chart)
            {
                var byteReader = YARGTextLoader.TryLoadByteText(file);
                if (byteReader != null)
                {
                    ParseDotChart(byteReader.Container, byteReader.Decoder, modifiers, ref parts, drums);
                }
                else
                {
                    var charReader = YARGTextLoader.LoadCharText(file);
                    ParseDotChart(charReader.Container, charReader.Decoder, modifiers, ref parts, drums);
                }
            }
            else // if (chartType == ChartType.Mid || chartType == ChartType.Midi) // Uncomment for any future file type
            {
                if (!ParseDotMidi(file, modifiers, ref parts, drums))
                {
                    return (ScanResult.MultipleMidiTrackNames, parts);
                }
            }

            SetDrums(ref parts, drums);

            if (!CheckScanValidity(in parts))
                return (ScanResult.NoNotes, parts);

            if (!modifiers.Contains("name"))
                return (ScanResult.NoName, parts);

            SetIntensities(modifiers, ref parts);
            return (ScanResult.Success, parts);
        }

        private static void ParseDotChart<TChar, TDecoder>(YARGTextContainer<TChar> reader, TDecoder decoder, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            if (YARGChartFileReader.ValidateTrack(reader, YARGChartFileReader.HEADERTRACK))
            {
                var chartMods = YARGChartFileReader.ExtractModifiers(reader, decoder, CHART_MODIFIER_LIST);
                modifiers.Append(chartMods);
            }

            while (YARGChartFileReader.IsStartOfTrack(reader))
            {
                if (!ParseChartTrack(reader, drums, ref parts))
                {
                    YARGTextReader.SkipLinesUntil(reader, '}');
                    if (!reader.IsEndOfFile())
                    {
                        YARGTextReader.GotoNextLine(reader);
                    }
                }
            }

            if (drums.Type == DrumsType.Unknown && drums.ValidatedDiffs > 0)
                drums.Type = DrumsType.FourLane;
        }

        private static bool ParseDotMidi(byte[] file, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
        {
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drums.Type == DrumsType.Unknown)
            {
                if (usePro)
                    drums.Type = DrumsType.UnknownPro;
            }
            else if (drums.Type == DrumsType.FourLane && usePro)
                drums.Type = DrumsType.ProDrums;

            return ParseMidi(file, drums, ref parts);
        }

        private static DrumsType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumsType.Unknown;
            return fivelane ? DrumsType.FiveLane : DrumsType.FourLane;
        }

        private static void SetIntensities(IniSection modifiers, ref AvailableParts parts)
        {
            if (modifiers.TryGet("diff_band", out int intensity))
            {
                parts.BandDifficulty.Intensity = (sbyte) intensity;
                if (intensity != -1)
                {
                    parts.BandDifficulty.SubTracks = 1;
                }
            }

            if (modifiers.TryGet("diff_guitar", out intensity))
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = parts.FiveFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bass", out intensity))
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = parts.FiveFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm", out intensity))
            {
                parts.FiveFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop", out intensity))
            {
                parts.FiveFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitarghl", out intensity))
            {
                parts.SixFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bassghl", out intensity))
            {
                parts.SixFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm_ghl", out intensity))
            {
                parts.SixFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop_ghl", out intensity))
            {
                parts.SixFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_keys", out intensity))
            {
                parts.ProKeys.Intensity = parts.Keys.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums", out intensity))
            {
                parts.FourLaneDrums.Intensity = (sbyte) intensity;
                parts.ProDrums.Intensity = (sbyte) intensity;
                parts.FiveLaneDrums.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums_real", out intensity))
            {
                parts.ProDrums.Intensity = (sbyte) intensity;
                if (parts.FourLaneDrums.Intensity == -1)
                {
                    parts.FourLaneDrums.Intensity = parts.ProDrums.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity))
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretGuitar.Intensity == -1)
                {
                    parts.FiveFretGuitar.Intensity = parts.ProGuitar_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real", out intensity))
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretBass.Intensity == -1)
                {
                    parts.FiveFretBass.Intensity = parts.ProBass_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real_22", out intensity))
            {
                parts.ProGuitar_22Fret.Intensity = (sbyte) intensity;
                if (parts.ProGuitar_17Fret.Intensity == -1)
                {
                    parts.ProGuitar_17Fret.Intensity = parts.ProGuitar_22Fret.Intensity;
                }

                if (parts.FiveFretGuitar.Intensity == -1)
                {
                    parts.FiveFretGuitar.Intensity = parts.ProGuitar_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real_22", out intensity))
            {
                parts.ProBass_22Fret.Intensity = (sbyte) intensity;
                if (parts.ProBass_17Fret.Intensity == -1)
                {
                    parts.ProBass_17Fret.Intensity = parts.ProBass_22Fret.Intensity;
                }

                if (parts.FiveFretBass.Intensity == -1)
                {
                    parts.FiveFretBass.Intensity = parts.ProBass_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_keys_real", out intensity))
            {
                parts.ProKeys.Intensity = (sbyte) intensity;
                if (parts.Keys.Intensity == -1)
                {
                    parts.Keys.Intensity = parts.ProKeys.Intensity;
                }
            }

            if (modifiers.TryGet("diff_vocals", out intensity))
            {
                parts.HarmonyVocals.Intensity = parts.LeadVocals.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_vocals_harm", out intensity))
            {
                parts.HarmonyVocals.Intensity = (sbyte) intensity;
                if (parts.LeadVocals.Intensity == -1)
                {
                    parts.LeadVocals.Intensity = parts.HarmonyVocals.Intensity;
                }
            }
        }

        protected static T? GetRandomBackgroundImage<T>(IEnumerable<KeyValuePair<string, T>> collection)
            where T : class
        {
            // Choose a valid image background present in the folder at random
            var images = new List<T>();
            foreach (var format in IMAGE_EXTENSIONS)
            {
                var (_, image) = collection.FirstOrDefault(node => node.Key == "bg" + format);
                if (image != null)
                {
                    images.Add(image);
                }
            }

            foreach (var (shortname, image) in collection)
            {
                if (!shortname.StartsWith("background"))
                {
                    continue;
                }

                foreach (var format in IMAGE_EXTENSIONS)
                {
                    if (shortname.EndsWith(format))
                    {
                        images.Add(image);
                        break;
                    }
                }
            }

            return images.Count > 0 ? images[BACKROUND_RNG.Next(images.Count)] : null;
        }
    }
}
