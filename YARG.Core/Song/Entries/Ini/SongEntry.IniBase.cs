using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
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

    internal abstract class IniSubEntry : SongEntry
    {
        public static readonly (string Filename, ChartFormat Format)[] CHART_FILE_TYPES =
        {
            ("notes.mid"  , ChartFormat.Mid),
            ("notes.midi" , ChartFormat.Midi),
            ("notes.chart", ChartFormat.Chart),
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
            for (int i = 0; i < PREVIEW_FILES.Length; i++)
            {
                PREVIEW_FILES[i] = "preview" + IniAudio.SupportedFormats[i];
            }
        }

        protected readonly string _location;
        protected readonly DateTime _chartLastWrite;
        protected readonly ChartFormat _chartFormat;
        protected string _background = string.Empty;
        protected string _video = string.Empty;
        protected string _cover = string.Empty;

        public override string SortBasedLocation => _location;
        public override string ActualLocation => _location;
        public override DateTime GetLastWriteTime() { return _chartLastWrite; }

        protected abstract FixedArray<byte>? GetChartData(string filename);

        internal override void Serialize(MemoryStream stream, CacheWriteIndices indices)
        {
            base.Serialize(stream, indices);
            stream.Write(_background);
            stream.Write(_video);
            stream.Write(_cover);
        }

        public override SongChart? LoadChart()
        {
            using var data = GetChartData(CHART_FILE_TYPES[(int) _chartFormat].Filename);
            if (data == null)
            {
                return null;
            }

            var parseSettings = new ParseSettings()
            {
                HopoThreshold = _settings.HopoThreshold,
                SustainCutoffThreshold = _settings.SustainCutoffThreshold,
                StarPowerNote = _settings.OverdiveMidiNote,
                DrumsType = ParseDrumsType(in _parts),
                ChordHopoCancellation = _chartFormat != ChartFormat.Chart
            };

            using var stream = data.ToReferenceStream();
            if (_chartFormat == ChartFormat.Mid || _chartFormat == ChartFormat.Midi)
            {
                return SongChart.FromMidi(in parseSettings, MidFileLoader.LoadMidiFile(stream));
            }

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(in parseSettings, reader.ReadToEnd());
        }

        public override FixedArray<byte>? LoadMiloData()
        {
            return null;
        }

        protected new void Deserialize(ref FixedArrayStream stream, CacheReadStrings strings)
        {
            base.Deserialize(ref stream, strings);
            _background = stream.ReadString();
            _video = stream.ReadString();
            _cover = stream.ReadString();
            (_parsedYear, _yearAsNumber) = ParseYear(_metadata.Year);
        }

        protected IniSubEntry(string location, in DateTime chartLastWrite, ChartFormat chartFormat)
        {
            _location = location;
            _chartLastWrite = chartLastWrite;
            _chartFormat = chartFormat;
        }

        protected internal static ScanResult ScanChart(IniSubEntry entry, FixedArray<byte> file, IniModifierCollection modifiers)
        {
            var drums_type = DrumsType.Any;
            if (modifiers.Extract("five_lane_drums", out bool fiveLaneDrums))
            {
                drums_type = fiveLaneDrums ? DrumsType.FiveLane : DrumsType.FourOrPro;
            }

            ScanExpected<long> resolution;
            if (entry._chartFormat == ChartFormat.Chart)
            {
                if (YARGTextReader.TryUTF8(file, out var byteContainer))
                {
                    resolution = ParseDotChart(ref byteContainer, modifiers, ref entry._parts, ref drums_type);
                }
                else
                {
                    using var chars = YARGTextReader.TryUTF16Cast(file);
                    if (chars != null)
                    {
                        var charContainer = YARGTextReader.CreateUTF16Container(chars);
                        resolution = ParseDotChart(ref charContainer, modifiers, ref entry._parts, ref drums_type);
                    }
                    else
                    {
                        using var ints = YARGTextReader.CastUTF32(file);
                        var intContainer = YARGTextReader.CreateUTF32Container(ints);
                        resolution = ParseDotChart(ref intContainer, modifiers, ref entry._parts, ref drums_type);
                    }
                }
            }
            else // if (chartType == ChartType.Mid || chartType == ChartType.Midi) // Uncomment for any future file type
            {
                resolution = ParseDotMidi(file, modifiers, ref entry._parts, ref drums_type);
            }

            if (!resolution)
            {
                return resolution.Error;
            }

            FinalizeDrums(ref entry._parts, drums_type);
            if (!IsValid(in entry._parts))
            {
                return ScanResult.NoNotes;
            }

            if (!modifiers.Contains("name"))
            {
                return ScanResult.NoName;
            }

            SongMetadata.FillFromIni(ref entry._metadata, modifiers);
            SetIntensities(modifiers, ref entry._parts);

            (entry._parsedYear, entry._yearAsNumber) = ParseYear(entry._metadata.Year);
            entry._hash = HashWrapper.Hash(file.ReadOnlySpan);
            entry.SetSortStrings();

            if (!modifiers.Extract("hopo_frequency", out entry._settings.HopoThreshold) || entry._settings.HopoThreshold <= 0)
            {
                if (modifiers.Extract("eighthnote_hopo", out bool eighthNoteHopo))
                {
                    entry._settings.HopoThreshold = resolution.Value / (eighthNoteHopo ? 2 : 3);
                }
                else if (modifiers.Extract("hopofreq", out int hopoFreq))
                {
                    long denominator = hopoFreq switch
                    {
                        0 => 24,
                        1 => 16,
                        2 => 12,
                        3 => 8,
                        4 => 6,
                        5 => 4,
                        _ => throw new NotImplementedException($"Unhandled hopofreq value {hopoFreq}!")
                    };
                    entry._settings.HopoThreshold = 4 * resolution.Value / denominator;
                }
                else
                {
                    entry._settings.HopoThreshold = resolution.Value / 3;
                }

                if (entry._chartFormat == ChartFormat.Chart)
                {
                    // With a 192 resolution, .chart has a HOPO threshold of 65 ticks, not 64,
                    // so we need to scale this factor to different resolutions (480 res = 162.5 threshold).
                    // Why?... idk, but I hate it.
                    const float DEFAULT_RESOLUTION = 192;
                    entry._settings.HopoThreshold += (long) (resolution.Value / DEFAULT_RESOLUTION);
                }
            }

            // .chart defaults to no sustain cutoff whatsoever if the ini does not define the value.
            // Since a failed `Extract` sets the value to zero, we need no additional work unless it's .mid
            if (!modifiers.Extract("sustain_cutoff_threshold", out entry._settings.SustainCutoffThreshold) && entry._chartFormat != ChartFormat.Chart)
            {
                entry._settings.SustainCutoffThreshold = resolution.Value / 3;
            }

            if (entry._chartFormat == ChartFormat.Mid || entry._chartFormat == ChartFormat.Midi)
            {
                if (!modifiers.Extract("multiplier_note", out entry._settings.OverdiveMidiNote) || entry._settings.OverdiveMidiNote != 103)
                {
                    entry._settings.OverdiveMidiNote = 116;
                }
            }

            if (modifiers.Extract("background", out string background))
            {
                entry._background = background;
            }

            if (modifiers.Extract("video", out string video))
            {
                entry._video = video;
            }

            if (modifiers.Extract("cover", out string cover))
            {
                entry._cover = cover;
            }

            if (entry._metadata.SongLength <= 0)
            {
                using var mixer = entry.LoadAudio(0, 0);
                if (mixer != null)
                {
                    entry._metadata.SongLength = (long) (mixer.Length * SongMetadata.MILLISECOND_FACTOR);
                }
            }
            return ScanResult.Success;
        }

        protected static bool TryGetRandomBackgroundImage<TValue>(Dictionary<string, TValue> dict, out TValue? value)
        {
            // Choose a valid image background present in the folder at random
            List<TValue>? images = null;
            foreach (var format in IMAGE_EXTENSIONS)
            {
                if (dict.TryGetValue("bg" + format, out var image))
                {
                    images ??= new List<TValue>();
                    images.Add(image);
                }
            }

            foreach (var (shortname, image) in dict)
            {
                if (!shortname.StartsWith("background"))
                {
                    continue;
                }

                foreach (var format in IMAGE_EXTENSIONS)
                {
                    if (shortname.EndsWith(format))
                    {
                        images ??= new List<TValue>();
                        images.Add(image);
                        break;
                    }
                }
            }

            if (images == null)
            {
                value = default!;
                return false;
            }
            value = images[BACKROUND_RNG.Next(images.Count)];
            return true;
        }

        protected static DrumsType ParseDrumsType(in AvailableParts parts)
        {
            if (parts.FourLaneDrums.IsActive())
            {
                return DrumsType.FourLane;
            }
            if (parts.FiveLaneDrums.IsActive())
            {
                return DrumsType.FiveLane;
            }
            return DrumsType.Unknown;
        }

        private static ScanExpected<long> ParseDotChart<TChar>(ref YARGTextContainer<TChar> container, IniModifierCollection modifiers, ref AvailableParts parts, ref DrumsType drumsType)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (drumsType != DrumsType.FiveLane && modifiers.Extract("pro_drums", out bool proDrums))
            {
                // We don't want to just immediately set the value to one of the other
                // on the chance that we still need to test for FiveLane.
                // We just know what the .ini explicitly tells us it *isn't*
                if (proDrums)
                {
                    drumsType -= DrumsType.FourLane;
                }
                else
                {
                    drumsType -= DrumsType.ProDrums;
                }
            }

            long resolution = 192;
            if (YARGChartFileReader.ValidateTrack(ref container, YARGChartFileReader.HEADERTRACK))
            {
                var chartMods = YARGChartFileReader.ExtractModifiers(ref container);
                if (chartMods.Extract("Resolution", out long res))
                {
                    resolution = res;
                    if (resolution < 1)
                    {
                        return new ScanUnexpected(ScanResult.InvalidResolution);
                    }
                }
                modifiers.Union(chartMods);
            }

            while (YARGChartFileReader.IsStartOfTrack(in container))
            {
                if (!TraverseChartTrack(ref container, ref parts, ref drumsType))
                {
                    YARGChartFileReader.SkipToNextTrack(ref container);
                }
            }
            return resolution;
        }

        private static ScanExpected<long> ParseDotMidi(FixedArray<byte> file, IniModifierCollection modifiers, ref AvailableParts parts, ref DrumsType drumsType)
        {
            if (drumsType != DrumsType.FiveLane)
            {
                // We don't want to just immediately set the value to one of the other
                // on the chance that we still need to test for FiveLane.
                // We just know what the .ini explicitly tells us it *isn't*.
                //
                // That being said, .chart differs in that FourLane is the default state.
                // .mid's default is ProDrums, which is why we account for when the .ini does
                // not contain the flag.
                if (!modifiers.Extract("pro_drums", out bool proDrums) || proDrums)
                {
                    drumsType -= DrumsType.FourLane;
                }
                else
                {
                    drumsType -= DrumsType.ProDrums;
                }
            }
            return ParseMidi(file, ref parts, ref drumsType);
        }

        /// <returns>Whether the track was fully traversed</returns>
        private static unsafe bool TraverseChartTrack<TChar>(ref YARGTextContainer<TChar> container, ref AvailableParts parts, ref DrumsType drumsType)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!YARGChartFileReader.ValidateInstrument(ref container, out var instrument, out var difficulty))
            {
                return false;
            }

            return instrument switch
            {
                Instrument.FiveFretGuitar     => ScanFiveFret(ref parts.FiveFretGuitar,               ref container, difficulty),
                Instrument.FiveFretBass       => ScanFiveFret(ref parts.FiveFretBass,                 ref container, difficulty),
                Instrument.FiveFretRhythm     => ScanFiveFret(ref parts.FiveFretRhythm,               ref container, difficulty),
                Instrument.FiveFretCoopGuitar => ScanFiveFret(ref parts.FiveFretCoopGuitar,           ref container, difficulty),
                Instrument.Keys               => ScanFiveFret(ref parts.Keys,                         ref container, difficulty),
                Instrument.SixFretGuitar      => ScanSixFret (ref parts.SixFretGuitar,                ref container, difficulty),
                Instrument.SixFretBass        => ScanSixFret (ref parts.SixFretBass,                  ref container, difficulty),
                Instrument.SixFretRhythm      => ScanSixFret (ref parts.SixFretRhythm,                ref container, difficulty),
                Instrument.SixFretCoopGuitar  => ScanSixFret (ref parts.SixFretCoopGuitar,            ref container, difficulty),
                Instrument.FourLaneDrums      => ScanDrums   (ref parts.FourLaneDrums, ref drumsType, ref container, difficulty),
                _ => false,
            };
        }

        private const int GUITAR_FIVEFRET_MAX = 5;
        private const int OPEN_NOTE = 7;
        private static bool ScanFiveFret<TChar>(ref PartValues part, ref YARGTextContainer<TChar> container, Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (part[difficulty])
            {
                return false;
            }

            var ev = default(DotChartEvent);
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    uint lane = YARGChartFileReader.ExtractWithWhitespace<TChar, uint>(ref container);
                    ulong _ = YARGChartFileReader.Extract<TChar, ulong>(ref container);
                    if (lane < GUITAR_FIVEFRET_MAX || lane == OPEN_NOTE)
                    {
                        part.ActivateDifficulty(difficulty);
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool ScanSixFret<TChar>(ref PartValues part, ref YARGTextContainer<TChar> container, Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            const int SIX_FRET_BLACK1 = 8;
            if (part[difficulty])
            {
                return false;
            }

            var ev = default(DotChartEvent);
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    uint lane = YARGChartFileReader.ExtractWithWhitespace<TChar, uint>(ref container);
                    ulong _ = YARGChartFileReader.Extract<TChar, ulong>(ref container);
                    if (lane < GUITAR_FIVEFRET_MAX || lane == SIX_FRET_BLACK1 || lane == OPEN_NOTE)
                    {
                        part.ActivateDifficulty(difficulty);
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool ScanDrums<TChar>(ref PartValues part, ref DrumsType drumsType, ref YARGTextContainer<TChar> container, Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            const int YELLOW_CYMBAL = 66;
            const int GREEN_CYMBAL = 68;
            const int DOUBLE_BASS_MODIFIER = 32;

            var diff_mask = (DifficultyMask)(1 << (int)difficulty);
            // No point in scan a difficulty that already exists
            if ((part.Difficulties & diff_mask) > DifficultyMask.None)
            {
                return false;
            }

            var requiredMask = diff_mask;
            if (difficulty == Difficulty.Expert)
            {
                requiredMask |= DifficultyMask.ExpertPlus;
            }

            var ev = default(DotChartEvent);
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    uint lane = YARGChartFileReader.ExtractWithWhitespace<TChar, uint>(ref container);
                    ulong _ = YARGChartFileReader.Extract<TChar, ulong>(ref container);
                    if (0 <= lane && lane <= 4)
                    {
                        part.Difficulties |= diff_mask;
                    }
                    else if (lane == 5)
                    {
                        // In other words, the DrumsType.FiveLane bit is active
                        if (drumsType >= DrumsType.FiveLane)
                        {
                            drumsType = DrumsType.FiveLane;
                            part.Difficulties |= diff_mask;
                        }
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        if ((drumsType & DrumsType.ProDrums) == DrumsType.ProDrums)
                        {
                            drumsType = DrumsType.ProDrums;
                        }
                    }
                    else if (lane == DOUBLE_BASS_MODIFIER)
                    {
                        if (difficulty == Difficulty.Expert)
                        {
                            part.Difficulties |= DifficultyMask.ExpertPlus;
                        }
                    }

                    //  Testing against zero would not work in expert
                    if ((part.Difficulties & requiredMask) == requiredMask && (drumsType == DrumsType.FourLane || drumsType == DrumsType.ProDrums || drumsType == DrumsType.FiveLane))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void SetIntensities(IniModifierCollection modifiers, ref AvailableParts parts)
        {
            if (modifiers.Extract("diff_band", out int intensity))
            {
                parts.BandDifficulty.Intensity = (sbyte) intensity;
                if (intensity != -1)
                {
                    parts.BandDifficulty.SubTracks = 1;
                }
            }

            if (modifiers.Extract("diff_guitar", out intensity))
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = parts.FiveFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_bass", out intensity))
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = parts.FiveFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_rhythm", out intensity))
            {
                parts.FiveFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_guitar_coop", out intensity))
            {
                parts.FiveFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_guitarghl", out intensity))
            {
                parts.SixFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_bassghl", out intensity))
            {
                parts.SixFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_rhythm_ghl", out intensity))
            {
                parts.SixFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_guitar_coop_ghl", out intensity))
            {
                parts.SixFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_keys", out intensity))
            {
                parts.ProKeys.Intensity = parts.Keys.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_drums", out intensity))
            {
                parts.FourLaneDrums.Intensity = (sbyte) intensity;
                parts.ProDrums.Intensity = (sbyte) intensity;
                parts.FiveLaneDrums.Intensity = (sbyte) intensity;
                parts.EliteDrums.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_drums_real", out intensity) && intensity != -1)
            {
                parts.ProDrums.Intensity = (sbyte) intensity;
                parts.EliteDrums.Intensity = (sbyte) intensity;
                if (parts.FourLaneDrums.Intensity == -1)
                {
                    parts.FourLaneDrums.Intensity = parts.ProDrums.Intensity;
                }
            }

            if (modifiers.Extract("diff_elite_drums", out intensity) && intensity != -1)
            {
                parts.EliteDrums.Intensity = (sbyte) intensity;

                if (parts.ProDrums.Intensity == -1)
                {
                    parts.ProDrums.Intensity = (sbyte) intensity;
                }

                if (parts.FourLaneDrums.Intensity == -1)
                {
                    parts.FourLaneDrums.Intensity = (sbyte) intensity;
                }

                if (parts.FiveLaneDrums.Intensity == -1)
                {
                    parts.FiveLaneDrums.Intensity = (sbyte) intensity;
                }
            }

            if (modifiers.Extract("diff_guitar_real", out intensity) && intensity != -1)
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretGuitar.Intensity == -1)
                {
                    parts.FiveFretGuitar.Intensity = parts.ProGuitar_17Fret.Intensity;
                }
            }

            if (modifiers.Extract("diff_bass_real", out intensity) && intensity != -1)
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretBass.Intensity == -1)
                {
                    parts.FiveFretBass.Intensity = parts.ProBass_17Fret.Intensity;
                }
            }

            if (modifiers.Extract("diff_guitar_real_22", out intensity) && intensity != -1)
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

            if (modifiers.Extract("diff_bass_real_22", out intensity) && intensity != -1)
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

            if (modifiers.Extract("diff_keys_real", out intensity) && intensity != -1)
            {
                parts.ProKeys.Intensity = (sbyte) intensity;
                if (parts.Keys.Intensity == -1)
                {
                    parts.Keys.Intensity = parts.ProKeys.Intensity;
                }
            }

            if (modifiers.Extract("diff_vocals", out intensity))
            {
                parts.HarmonyVocals.Intensity = parts.LeadVocals.Intensity = (sbyte) intensity;
            }

            if (modifiers.Extract("diff_vocals_harm", out intensity) && intensity != -1)
            {
                parts.HarmonyVocals.Intensity = (sbyte) intensity;
                if (parts.LeadVocals.Intensity == -1)
                {
                    parts.LeadVocals.Intensity = parts.HarmonyVocals.Intensity;
                }
            }
        }

        private static (string Parsed, int AsNumber) ParseYear(string str)
        {
            const int MINIMUM_YEAR_DIGITS = 4;
            for (int start = 0; start <= str.Length - MINIMUM_YEAR_DIGITS; ++start)
            {
                int curr = start;
                int number = 0;
                while (curr < str.Length && char.IsDigit(str[curr]))
                {
                    unchecked
                    {
                        number = 10 * number + str[curr] - '0';
                    }
                    ++curr;
                }

                if (curr >= start + MINIMUM_YEAR_DIGITS)
                {
                    return (str[start..curr], number);
                }
            }
            return (str, int.MaxValue);
        }
    }
}
