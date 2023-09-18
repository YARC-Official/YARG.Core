using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public sealed class YARGChartFileReader_Char : IYARGChartReader
    {
        private struct EventCombo
        {
            public string descriptor;
            public ChartEventType eventType;
            public EventCombo(string str, ChartEventType chartEvent)
            {
                descriptor = str;
                eventType = chartEvent;
            }
        }

        private const string HEADERTRACK = "[Song]";
        private const string SYNCTRACK =   "[SyncTrack]";
        private const string EVENTTRACK =  "[Events]";
        private static readonly EventCombo TEMPO =   new("B",  ChartEventType.Bpm);
        private static readonly EventCombo TIMESIG = new("TS", ChartEventType.Time_Sig);
        private static readonly EventCombo ANCHOR =  new("A",  ChartEventType.Anchor);
        private static readonly EventCombo TEXT =    new("E",  ChartEventType.Text);
        private static readonly EventCombo NOTE =    new("N",  ChartEventType.Note);
        private static readonly EventCombo SPECIAL = new("S",  ChartEventType.Special);

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

        private static readonly EventCombo[] EVENTS_SYNC = { TEMPO, TIMESIG, ANCHOR };
        private static readonly EventCombo[] EVENTS_EVENTS = { TEXT, };
        private static readonly EventCombo[] EVENTS_DIFF = { NOTE, SPECIAL, TEXT, };

        static YARGChartFileReader_Char() { }

        private readonly YARGTXTReader_Char reader;
        private readonly char[] data;
        private readonly int length;

        private EventCombo[] eventSet = Array.Empty<EventCombo>();
        private NoteTracks_Chart _instrument;
        private Difficulty _difficulty;

        public NoteTracks_Chart Instrument => _instrument;
        public Difficulty Difficulty => _difficulty;

        public YARGChartFileReader_Char(YARGTXTReader_Char reader)
        {
            this.reader = reader;
            data = reader.Data;
            length = data.Length;
        }

        public YARGChartFileReader_Char(char[] data) : this(new YARGTXTReader_Char(data)) { }

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

        private bool ValidateTrack(string track)
        {
            if (!DoesStringMatch(track))
                return false;

            reader.GotoNextLine();
            return true;
        }

        private bool DoesStringMatch(string str)
        {
            if (reader.Next - reader.Position < str.Length)
                return false;
            return reader.ExtractBasicSpan(str.Length).SequenceEqual(str);
        }

        public bool IsStillCurrentTrack()
        {
            int position = reader.Position;
            if (position == length)
                return false;

            if (data[position] == '}')
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

            int start, length;
            bool EqualSequences(string descriptor)
            {
                if (descriptor.Length != length) return false;
                for (int i = 0; i < length; ++i)
                    if (descriptor[i] != data[start + i]) return false;
                return true;
            }

            ev.Position = reader.ReadInt64();

            int end = reader.Position;
            start = end;
            while (true)
            {
                byte curr = (byte) (data[end] & LOWER_CASE_MASK);
                if (curr < 'A' || 'Z' < curr)
                    break;
                ++end;
            }

            length = end - start;
            reader.Position = end;
            foreach (var combo in eventSet)
                if (EqualSequences(combo.descriptor))
                {
                    reader.SkipWhiteSpace();
                    ev.Type = combo.eventType;
                    return true;
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
                while (point > position && ITXTReader.IsWhitespace(data[point]) && data[point] != '\n')
                    --point;

                if (data[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    reader.GotoNextLine();
                    return;
                }

                position += next + 1;
            }

            reader.Position = length;
            reader.SetNextPointer();
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (data[position + i] == '}')
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
                var name = reader.ExtractModifierName();
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
