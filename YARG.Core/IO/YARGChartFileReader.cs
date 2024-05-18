using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private long _position;

        public ChartEventType Type;
        public long Position
        {
            get { return _position; }
            set
            {
                if (_position <= value)
                    _position = value;
                else
                    throw new Exception($".chart position out of order (previous: {_position})");
            }
        }
    }

    public struct DotChartNote
    {
        public int Lane;
        public long Duration;
    }

    public static class YARGChartFileReader
    {
        public const string HEADERTRACK = "[Song]";
        public const string SYNCTRACK =   "[SyncTrack]";
        public const string EVENTTRACK =  "[Events]";

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
            return !container.IsEndOfFile() && container.IsCurrentCharacter('[');
        }

        public static bool ValidateTrack<TChar>(ref YARGTextContainer<TChar> container, string track)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!DoesStringMatch(ref container, track))
                return false;

            YARGTextReader.GotoNextLine(ref container);
            return true;
        }

        public static bool ValidateInstrument<TChar>(ref YARGTextContainer<TChar> container, out Instrument instrument, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (ValidateDifficulty(ref container, out difficulty))
            {
                foreach (var (name, inst) in YARGChartFileReader.NOTETRACKS)
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

        private static bool ValidateDifficulty<TChar>(ref YARGTextContainer<TChar> container, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            for (int diffIndex = 3; diffIndex >= 0; --diffIndex)
            {
                var (name, diff) = YARGChartFileReader.DIFFICULTIES[diffIndex];
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
            int index = 0;
            int position = container.Position;
            while (index < str.Length
                && position < container.Length
                && container.Data[position].ToChar(null) == str[index])
            {
                ++index;
                ++position;
            }
            return index == str.Length;
        }

        public static bool IsStillCurrentTrack<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YARGTextReader.GotoNextLine(ref container);
            if (container.Position == container.Length)
                return false;

            if (container.IsCurrentCharacter('}'))
            {
                YARGTextReader.GotoNextLine(ref container);
                return false;
            }

            return true;
        }

        public static bool TryParseEvent<TChar>(ref YARGTextContainer<TChar> container, ref DotChartEvent ev)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!IsStillCurrentTrack(ref container))
            {
                return false;
            }

            ev.Position = YARGTextReader.ExtractInt64(ref container);
            ev.Type = ChartEventType.Unknown;

            var curr = container.Position;
            while (curr < container.Length)
            {
                int c = container.Data[curr].ToChar(null) | CharacterExtensions.ASCII_LOWERCASE_FLAG;
                if (c < 'a' || 'z' < c)
                {
                    break;
                }
                ++curr;
            }

            var span = new ReadOnlySpan<TChar>(container.Data, container.Position, curr - container.Position);
            container.Position = curr;
            foreach (var combo in EVENTS)
            {
                if (span.Length != combo.Descriptor.Length)
                {
                    continue;
                }

                int index = 0;
                while (index < span.Length && span[index].ToChar(null) == combo.Descriptor[index])
                {
                    ++index;
                }

                if (index == span.Length)
                {
                    YARGTextReader.SkipWhitespace(ref container);
                    ev.Type = combo.Type;
                    break;
                }
            }
            return true;
        }

        public unsafe static Dictionary<string, List<IniModifier>> ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container, delegate*<TChar[], int, int, string> decoder, Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentTrack(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container, decoder);
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(ref container, decoder);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
            }
            return modifiers;
        }
    }
}
