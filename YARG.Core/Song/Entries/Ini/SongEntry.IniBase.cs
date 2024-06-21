using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public readonly struct IniChartNode<T>
    {
        public readonly T File;
        public readonly ChartType Type;

        public IniChartNode(T file, ChartType type)
        {
            File = file;
            Type = type;
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
            new("notes.mid"  , ChartType.Mid),
            new("notes.midi" , ChartType.Midi),
            new("notes.chart", ChartType.Chart),
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

        protected IniSubEntry(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            _background = stream.ReadString();
            _video = stream.ReadString();
            _cover = stream.ReadString();
            Video_Loop = stream.ReadBoolean();
        }

        protected abstract Stream? GetChartStream();

        protected abstract void SerializeSubData(BinaryWriter writer);

        public ReadOnlySpan<byte> Serialize(CategoryCacheWriteNode node, string groupDirectory)
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
            return new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length);
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

        public override FixedArray<byte>? LoadMiloData()
        {
            return null;
        }

        protected static (ScanResult Result, AvailableParts Parts) ScanIniChartFile(FixedArray<byte> file, ChartType chartType, IniSection modifiers)
        {
            DrumPreparseHandler drums = new()
            {
                Type = GetDrumTypeFromModifier(modifiers)
            };

            var parts = AvailableParts.Default;
            if (chartType == ChartType.Chart)
            {
                if (YARGTextReader.IsUTF8(file, out var byteContainer))
                {
                    ParseDotChart(ref byteContainer, modifiers, ref parts, drums);
                }
                else
                {
                    using var chars = YARGTextReader.ConvertToUTF16(file, out var charContainer);
                    if (chars != null)
                    {
                        ParseDotChart(ref charContainer, modifiers, ref parts, drums);
                    }
                    else
                    {
                        using var ints = YARGTextReader.ConvertToUTF32(file, out var intContainer);
                        ParseDotChart(ref intContainer, modifiers, ref parts, drums);
                    }
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

        private static void ParseDotChart<TChar>(ref YARGTextContainer<TChar> container, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (YARGChartFileReader.ValidateTrack(ref container, YARGChartFileReader.HEADERTRACK))
            {
                var chartMods = YARGChartFileReader.ExtractModifiers(ref container, CHART_MODIFIER_LIST);
                modifiers.Append(chartMods);
            }

            while (YARGChartFileReader.IsStartOfTrack(in container))
            {
                if (!ParseChartTrack(ref container, drums, ref parts))
                {
                    if (YARGTextReader.SkipLinesUntil(ref container, '}'))
                    {
                        YARGTextReader.GotoNextLine(ref container);
                    }
                }
            }

            if (drums.Type == DrumsType.Unknown && drums.ValidatedDiffs > 0)
                drums.Type = DrumsType.FourLane;
        }

        private static bool ParseDotMidi(FixedArray<byte> file, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
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

        private static bool ParseChartTrack<TChar>(ref YARGTextContainer<TChar> container, DrumPreparseHandler drums, ref AvailableParts parts)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!YARGChartFileReader.ValidateInstrument(ref container, out var instrument, out var difficulty))
            {
                return false;
            }

            return instrument switch
            {
                Instrument.FiveFretGuitar =>     ChartPreparser.Preparse(ref container, difficulty, ref parts.FiveFretGuitar,     ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretBass =>       ChartPreparser.Preparse(ref container, difficulty, ref parts.FiveFretBass,       ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretRhythm =>     ChartPreparser.Preparse(ref container, difficulty, ref parts.FiveFretRhythm,     ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretCoopGuitar => ChartPreparser.Preparse(ref container, difficulty, ref parts.FiveFretCoopGuitar, ChartPreparser.ValidateFiveFret),
                Instrument.SixFretGuitar =>      ChartPreparser.Preparse(ref container, difficulty, ref parts.SixFretGuitar,      ChartPreparser.ValidateSixFret),
                Instrument.SixFretBass =>        ChartPreparser.Preparse(ref container, difficulty, ref parts.SixFretBass,        ChartPreparser.ValidateSixFret),
                Instrument.SixFretRhythm =>      ChartPreparser.Preparse(ref container, difficulty, ref parts.SixFretRhythm,      ChartPreparser.ValidateSixFret),
                Instrument.SixFretCoopGuitar =>  ChartPreparser.Preparse(ref container, difficulty, ref parts.SixFretCoopGuitar,  ChartPreparser.ValidateSixFret),
                Instrument.Keys =>               ChartPreparser.Preparse(ref container, difficulty, ref parts.Keys,               ChartPreparser.ValidateFiveFret),
                Instrument.FourLaneDrums =>      drums.ParseChart(ref container, difficulty),
                _ => false,
            };
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

        protected static bool TryGetRandomBackgroundImage<T>(IEnumerable<KeyValuePair<string, T>> collection, out T value)
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

            if (images.Count == 0)
            {
                value = default!;
                return false;
            }
            value = images[BACKROUND_RNG.Next(images.Count)];
            return true;
        }
    }
}
