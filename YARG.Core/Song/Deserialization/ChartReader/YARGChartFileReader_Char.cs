using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public sealed class YARGChartFileReader_Char : IYARGChartReader
    {
        private const string HEADERTRACK = "[Song]";
        private const string SYNCTRACK =   "[SyncTrack]";
        private const string EVENTTRACK =  "[Events]";
        private static readonly DotChartEventCombo<char> TEMPO =   new("B".ToCharArray(),  ChartEventType.Bpm);
        private static readonly DotChartEventCombo<char> TIMESIG = new("TS".ToCharArray(), ChartEventType.Time_Sig);
        private static readonly DotChartEventCombo<char> ANCHOR =  new("A".ToCharArray(),  ChartEventType.Anchor);
        private static readonly DotChartEventCombo<char> TEXT =    new("E".ToCharArray(),  ChartEventType.Text);
        private static readonly DotChartEventCombo<char> NOTE =    new("N".ToCharArray(),  ChartEventType.Note);
        private static readonly DotChartEventCombo<char> SPECIAL = new("S".ToCharArray(),  ChartEventType.Special);

        private static readonly (string name, Difficulty difficulty)[] DIFFICULTIES =
        {
            ("[Easy", Difficulty.Easy),
            ("[Medium", Difficulty.Medium),
            ("[Hard", Difficulty.Hard),
            ("[Expert", Difficulty.Expert),
        };

        private static readonly (string, NoteTracks_Chart)[] NOTETRACKS =
        {
            new("Single]",       NoteTracks_Chart.Single ),
            new("DoubleGuitar]", NoteTracks_Chart.DoubleGuitar ),
            new("DoubleBass]",   NoteTracks_Chart.DoubleBass ),
            new("DoubleRhythm]", NoteTracks_Chart.DoubleRhythm ),
            new("Drums]",        NoteTracks_Chart.Drums ),
            new("Keyboard]",     NoteTracks_Chart.Keys ),
            new("GHLGuitar]",    NoteTracks_Chart.GHLGuitar ),
            new("GHLBass]",      NoteTracks_Chart.GHLBass ),
            new("GHLRhythm]",    NoteTracks_Chart.GHLGuitar ),
            new("GHLCoop]",      NoteTracks_Chart.GHLBass ),
        };

        private static readonly DotChartEventCombo<char>[] EVENTS_SYNC = { TEMPO, TIMESIG, ANCHOR };
        private static readonly DotChartEventCombo<char>[] EVENTS_EVENTS = { TEXT, };
        private static readonly DotChartEventCombo<char>[] EVENTS_DIFF = { NOTE, SPECIAL, TEXT, };

        static YARGChartFileReader_Char() { }

        private readonly YARGTXTReader_Char reader;

        private DotChartEventCombo<char>[] eventSet = Array.Empty<DotChartEventCombo<char>>();
        private NoteTracks_Chart _instrument;
        private Difficulty _difficulty;

        public NoteTracks_Chart Instrument => _instrument;
        public Difficulty Difficulty => _difficulty;

        public YARGChartFileReader_Char(YARGTXTReader_Char reader)
        {
            this.reader = reader;
        }

        public YARGChartFileReader_Char(char[] reader) : this(new YARGTXTReader_Char(reader)) { }

        public YARGChartFileReader_Char(string path) : this(new YARGTXTReader_Char(path)) { }

        public bool IsStartOfTrack()
        {
            return !reader.IsEndOfFile() && reader.Peek() == '[';
        }

        public bool ValidateHeaderTrack()
        {
            return ValidateTrack(HEADERTRACK);
        }

        public bool ValidateSyncTrack()
        {
            if (!ValidateTrack(SYNCTRACK))
                return false;

            eventSet = EVENTS_SYNC;
            return true;
        }

        public bool ValidateEventsTrack()
        {
            if (!ValidateTrack(EVENTTRACK))
                return false;

            eventSet = EVENTS_EVENTS;
            return true;
        }

        public bool ValidateDifficulty()
        {
            for (int diff = 3; diff >= 0; --diff)
            {
                var (name, difficulty) = DIFFICULTIES[diff];
                if (DoesStringMatch(name))
                {
                    _difficulty = difficulty;
                    eventSet = EVENTS_DIFF;
                    reader.Position += name.Length;
                    return true;
                }
            }
            return false;
        }

        public bool ValidateInstrument()
        {
            foreach (var track in NOTETRACKS)
            {
                if (ValidateTrack(track.Item1))
                {
                    _instrument = track.Item2;
                    return true;
                }
            }
            return false;
        }

        private bool ValidateTrack(ReadOnlySpan<char> track)
        {
            if (!DoesStringMatch(track))
                return false;

            reader.GotoNextLine();
            return true;
        }

        private bool DoesStringMatch(ReadOnlySpan<char> str)
        {
            if (reader.Next - reader.Position < str.Length)
                return false;
            return reader.ExtractBasicSpan(str.Length).SequenceEqual(str);
        }

        public bool IsStillCurrentTrack()
        {
            int position = reader.Position;
            if (position == reader.Length)
                return false;

            if (reader.Data[position] == '}')
            {
                reader.GotoNextLine();
                return false;
            }

            return true;
        }

        private const int LOWER_CASE_MASK = ~32;

        public bool TryParseEvent(ref DotChartEvent ev)
        {
            if (!IsStillCurrentTrack())
                return false;

            ev.Position = reader.ReadInt64();

            int start = reader.Position;
            int end = start;
            while (true)
            {
                char curr = (char) (reader.Data[end] & LOWER_CASE_MASK);
                if (curr < 'A' || 'Z' < curr)
                    break;
                ++end;
            }
            reader.Position = end;

            ReadOnlySpan<char> span = new(reader.Data, start, end - start);
            foreach (var combo in eventSet)
            {
                if (combo.DoesEventMatch(span))
                {
                    reader.SkipWhiteSpace();
                    ev.Type = combo.eventType;
                    return true;
                }
            }

            ev.Type = ChartEventType.Unknown;
            return true;
        }

        public void SkipEvent()
        {
            reader.GotoNextLine();
        }

        public void NextEvent()
        {
            reader.GotoNextLine();
        }

        public void ExtractLaneAndSustain(ref DotChartNote note)
        {
            note.Lane = reader.ReadInt32();
            note.Duration = reader.ReadInt64();
        }

        public void SkipTrack()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && ITXTReader.IsWhitespace(reader.Data[point]) && reader.Data[point] != '\n')
                    --point;

                if (reader.Data[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    reader.GotoNextLine();
                    return;
                }

                position += next + 1;
            }

            reader.Position = reader.Length;
            reader.SetNextPointer();
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = reader.Length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (reader.Data[position + i] == '}')
                    return true;
                ++i;
            }
            return false;
        }

        public Dictionary<string, List<IniModifier>> ExtractModifiers(Dictionary<string, IniModifierCreator> validNodes)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentTrack())
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
