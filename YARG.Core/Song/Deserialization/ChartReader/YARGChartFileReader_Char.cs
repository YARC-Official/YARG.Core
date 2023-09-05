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
            public ChartEvent_FW eventType;
            public EventCombo(string str, ChartEvent_FW chartEvent)
            {
                descriptor = str;
                eventType = chartEvent;
            }
        }

        private const string HEADERTRACK = "[Song]";
        private const string SYNCTRACK =   "[SyncTrack]";
        private const string EVENTTRACK =  "[Events]";
        private static readonly EventCombo TEMPO =   new("B",  ChartEvent_FW.BPM);
        private static readonly EventCombo TIMESIG = new("TS", ChartEvent_FW.TIME_SIG);
        private static readonly EventCombo ANCHOR =  new("A",  ChartEvent_FW.ANCHOR);
        private static readonly EventCombo TEXT =    new("E",  ChartEvent_FW.EVENT);
        private static readonly EventCombo NOTE =    new("N",  ChartEvent_FW.NOTE);
        private static readonly EventCombo SPECIAL = new("S",  ChartEvent_FW.SPECIAL);

        private static readonly string[] DIFFICULTIES =
        {
            "[Easy",
            "[Medium",
            "[Hard",
            "[Expert"
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
        private long tickPosition = 0;
        private NoteTracks_Chart _instrument;
        private int _difficulty;

        public NoteTracks_Chart Instrument => _instrument;
        public int Difficulty => _difficulty;

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
                if (DoesStringMatch(DIFFICULTIES[diff]))
                {
                    _difficulty = diff;
                    eventSet = EVENTS_DIFF;
                    reader.Position += DIFFICULTIES[diff].Length;
                    return true;
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
            tickPosition = 0;
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

        public (long, ChartEvent_FW) ParseEvent()
        {
            int start, length;
            bool EqualSequences(string descriptor)
            {
                if (descriptor.Length != length) return false;
                for (int i = 0; i < length; ++i)
                    if (descriptor[i] != data[start + i]) return false;
                return true;
            }

            long position = reader.ReadInt64();
            if (position < tickPosition)
                throw new Exception($".chart position out of order (previous: {tickPosition})");

            tickPosition = position;

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
                    return new(position, combo.eventType);
                }
            return new(position, ChartEvent_FW.UNKNOWN);
        }

        public void SkipEvent()
        {
            reader.GotoNextLine();
        }

        public void NextEvent()
        {
            reader.GotoNextLine();
        }

        public (int, long) ExtractLaneAndSustain()
        {
            int lane = reader.ReadInt32();
            long sustain = reader.ReadInt64();
            return new(lane, sustain);
        }

        public void SkipTrack()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && YARGTXTReader_Base<char>.IsWhitespace(data[point]) && data[point] != '\n')
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
