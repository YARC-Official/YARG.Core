using System;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;

namespace YARG.Core.IO
{
    public enum ChartEventType
    {
        Bpm,
        Time_Sig,
        Anchor,
        Text,
        Note,
        Special,
        Unknown = 255,
    }

    public enum NoteTracks_Chart
    {
        Single,
        DoubleGuitar,
        DoubleBass,
        DoubleRhythm,
        Drums,
        Keys,
        GHLGuitar,
        GHLBass,
        GHLRhythm,
        GHLCoop,
        Invalid,
    };

    public struct DotChartEvent
    {
        public long Position;
        public ChartEventType Type;
    }

    public static class YARGChartFileReader
    {
        public const string HEADERTRACK = "[Song]";
        public const string SYNCTRACK = "[SyncTrack]";
        public const string EVENTTRACK = "[Events]";

        internal static readonly (string name, Difficulty difficulty)[] DIFFICULTIES =
        {
            ("[Easy", Difficulty.Easy),
            ("[Medium", Difficulty.Medium),
            ("[Hard", Difficulty.Hard),
            ("[Expert", Difficulty.Expert),
        };

        internal static readonly (string, Instrument)[] NOTETRACKS =
        {
            new("Single]",       Instrument.FiveFretGuitar),
            new("DoubleGuitar]", Instrument.FiveFretCoopGuitar),
            new("DoubleBass]",   Instrument.FiveFretBass),
            new("DoubleRhythm]", Instrument.FiveFretRhythm),
            new("Drums]",        Instrument.FourLaneDrums),
            new("Keyboard]",     Instrument.Keys),
            new("GHLGuitar]",    Instrument.SixFretGuitar),
            new("GHLBass]",      Instrument.SixFretBass),
            new("GHLRhythm]",    Instrument.SixFretRhythm),
            new("GHLCoop]",      Instrument.SixFretCoopGuitar),
        };

        internal static readonly (string Descriptor, ChartEventType Type)[] EVENTS =
        {
            new("B",  ChartEventType.Bpm),
            new("TS", ChartEventType.Time_Sig),
            new("A",  ChartEventType.Anchor),
            new("E",  ChartEventType.Text),
            new("N",  ChartEventType.Note),
            new("S",  ChartEventType.Special),
        };

        public static bool IsStartOfTrack<TChar>(in YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            return !container.IsAtEnd() && container.Get() == '[';
        }

        public static bool IsStillCurrentTrack<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YARGTextReader.GotoNextLine(ref container);
            if (container.IsAtEnd())
            {
                return false;
            }

            if (container.Get() == '}')
            {
                YARGTextReader.GotoNextLine(ref container);
                return false;
            }
            return true;
        }

        public static void SkipToNextTrack<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.CLOSE_BRACE))
            {
                YARGTextReader.GotoNextLine(ref container);
            }
        }

        public static bool ValidateTrack<TChar>(ref YARGTextContainer<TChar> container, string track)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!DoesStringMatch(ref container, track))
            {
                return false;
            }
            YARGTextReader.GotoNextLine(ref container);
            return true;
        }

        public static bool ValidateInstrument<TChar>(ref YARGTextContainer<TChar> container, out Instrument instrument, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (ValidateDifficulty(ref container, out difficulty))
            {
                foreach (var (name, inst) in NOTETRACKS)
                {
                    if (ValidateTrack(ref container, name))
                    {
                        instrument = inst;
                        return true;
                    }
                }
            }
            instrument = default;
            return false;
        }

        public static bool TryParseEvent<TChar>(ref YARGTextContainer<TChar> container, ref DotChartEvent ev)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!IsStillCurrentTrack(ref container))
            {
                return false;
            }

            if (!YARGTextReader.TryExtract(ref container, out long position))
            {
                throw new Exception("Could not parse event position");
            }

            if (position < ev.Position)
            {
                throw new Exception($".chart position out of order (previous: {ev.Position})");
            }

            ev.Position = position;
            YARGTextReader.SkipWhitespaceAndEquals(ref container);

            int length = 0;
            while (container.Position + length < container.Length)
            {
                int c = container[length];
                if (c < 'A' || 'Z' < c)
                {
                    break;
                }
                ++length;
            }

            ev.Type = ChartEventType.Unknown;
            foreach (var (descriptor, type) in EVENTS)
            {
                if (length == descriptor.Length)
                {
                    int index = 0;
                    while (index < length && container[index] == descriptor[index])
                    {
                        ++index;
                    }

                    if (index == descriptor.Length)
                    {
                        ev.Type = type;
                        break;
                    }
                }
            }

            container.Position += length;
            if (ev.Type != ChartEventType.Unknown)
            {
                YARGTextReader.SkipWhitespace(ref container);
            }
            return true;
        }

        public static TNumber Extract<TChar, TNumber>(ref YARGTextContainer<TChar> text)
            where TChar : unmanaged, IConvertible
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            if (!YARGTextReader.TryExtract(ref text, out TNumber value))
            {
                throw new Exception("Could not extract " + typeof(TNumber).Name);
            }
            return value;
        }

        public static TNumber ExtractWithWhitespace<TChar, TNumber>(ref YARGTextContainer<TChar> text)
            where TChar : unmanaged, IConvertible
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            if (!YARGTextReader.TryExtractWithWhitespace(ref text, out TNumber value))
            {
                throw new Exception("Could not extract " + typeof(TNumber).Name);
            }
            return value;
        }

        public static readonly Dictionary<string, IniModifierOutline> CHART_MODIFIERS = new()
        {
            { "Album",        new("album", ModifierType.String) },
            { "Artist",       new("artist", ModifierType.String) },
            { "Charter",      new("charter", ModifierType.String) },
            { "Difficulty",   new("diff_band", ModifierType.Int32) },
            { "Genre",        new("genre", ModifierType.String) },
            { "Name",         new("name", ModifierType.String) },
            { "Offset",       new("delay_seconds", ModifierType.Double) },
            { "PreviewEnd",   new("preview_end_seconds", ModifierType.Double) },
            { "PreviewStart", new("preview_start_seconds", ModifierType.Double) },
            { "Resolution",   new("Resolution", ModifierType.Int64) },
            { "Year",         new("year_chart", ModifierType.String) },
        };

        public static IniModifierCollection ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            IniModifierCollection collection = new();
            while (IsStillCurrentTrack(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container);
                if (CHART_MODIFIERS.TryGetValue(name, out var outline))
                {
                    collection.Add(ref container, outline, true);
                }
            }
            return collection;
        }

        private static bool ValidateDifficulty<TChar>(ref YARGTextContainer<TChar> container, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            for (int diffIndex = 3; diffIndex >= 0; --diffIndex)
            {
                var (name, diff) = DIFFICULTIES[diffIndex];
                if (DoesStringMatch(ref container, name))
                {
                    difficulty = diff;
                    container.Position += name.Length;
                    return true;
                }
            }
            difficulty = default;
            return false;
        }

        private static bool DoesStringMatch<TChar>(ref YARGTextContainer<TChar> container, string str)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (container.Length - container.Position < str.Length)
            {
                return false;
            }

            int index = 0;
            while (index < str.Length && container[index] == str[index])
            {
                ++index;
            }
            return index == str.Length;
        }
    }
}