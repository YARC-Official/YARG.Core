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

        public static bool IsStartOfTrack<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            return !reader.Container.IsEndOfFile() && reader.Container.IsCurrentCharacter('[');
        }

        public static bool ValidateTrack<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, string track)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            if (!DoesStringMatch(reader, track))
                return false;

            reader.GotoNextLine();
            return true;
        }

        public static bool ValidateInstrument<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, out Instrument instrument, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            if (ValidateDifficulty(reader, out difficulty))
            {
                foreach (var (name, inst) in YARGChartFileReader.NOTETRACKS)
                {
                    if (ValidateTrack(reader, name))
                    {
                        instrument = inst;
                        return true;
                    }
                }
            }
            instrument = default;
            return false;
        }

        private static bool ValidateDifficulty<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            for (int diffIndex = 3; diffIndex >= 0; --diffIndex)
            {
                var (name, diff) = YARGChartFileReader.DIFFICULTIES[diffIndex];
                if (DoesStringMatch(reader, name))
                {
                    difficulty = diff;
                    reader.Container.Position += name.Length;
                    return true;
                }
            }
            difficulty = default;
            return false;
        }

        private static bool DoesStringMatch<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, string str)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            int index = 0;
            int position = reader.Container.Position;
            while (index < str.Length
                && position < reader.Container.Length
                && reader.Container.Data[position].ToChar(null) == str[index])
            {
                ++index;
                ++position;
            }
            return index == str.Length;
        }

        public static bool IsStillCurrentTrack<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            int position = reader.Container.Position;
            if (position == reader.Container.Length)
                return false;

            if (reader.Container.IsCurrentCharacter('}'))
            {
                reader.GotoNextLine();
                return false;
            }

            return true;
        }

        public static bool TryParseEvent<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, ref DotChartEvent ev)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            if (!IsStillCurrentTrack(reader))
            {
                return false;
            }

            ev.Position = reader.ExtractInt64();
            ev.Type = ChartEventType.Unknown;

            var curr = reader.Container.Position;
            while (curr < reader.Container.Length)
            {
                int c = reader.Container.Data[curr].ToChar(null) | CharacterExtensions.ASCII_LOWERCASE_FLAG;
                if (c < 'a' || 'z' < c)
                {
                    break;
                }
                ++curr;
            }

            var span = new ReadOnlySpan<TChar>(reader.Container.Data, reader.Container.Position, curr - reader.Container.Position);
            reader.Container.Position = curr;
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
                    reader.SkipWhitespace();
                    ev.Type = combo.Type;
                    break;
                }
            }
            return true;
        }

        public static Dictionary<string, List<IniModifier>> ExtractModifiers<TChar, TDecoder>(YARGTextReader<TChar, TDecoder> reader, Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentTrack(reader))
            {
                string name = reader.ExtractModifierName();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(reader);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return modifiers;
        }
    }
}
