using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public sealed class YARGChartFileReader : IYARGChartReader
    {
        private static readonly byte[] HEADERTRACK = Encoding.ASCII.GetBytes("[Song]");
        private static readonly byte[] SYNCTRACK =   Encoding.ASCII.GetBytes("[SyncTrack]");
        private static readonly byte[] EVENTTRACK =  Encoding.ASCII.GetBytes("[Events]");
        private static readonly DotChartEventCombo<byte> TEMPO =   new(Encoding.ASCII.GetBytes("B"),  ChartEventType.Bpm);
        private static readonly DotChartEventCombo<byte> TIMESIG = new(Encoding.ASCII.GetBytes("TS"), ChartEventType.Time_Sig);
        private static readonly DotChartEventCombo<byte> ANCHOR =  new(Encoding.ASCII.GetBytes("A"),  ChartEventType.Anchor);
        private static readonly DotChartEventCombo<byte> TEXT =    new(Encoding.ASCII.GetBytes("E"),  ChartEventType.Text);
        private static readonly DotChartEventCombo<byte> NOTE =    new(Encoding.ASCII.GetBytes("N"),  ChartEventType.Note);
        private static readonly DotChartEventCombo<byte> SPECIAL = new(Encoding.ASCII.GetBytes("S"),  ChartEventType.Special);

        private static readonly (byte[] name, Difficulty difficulty)[] DIFFICULTIES =
        {
            (Encoding.ASCII.GetBytes("[Easy"), Difficulty.Easy),
            (Encoding.ASCII.GetBytes("[Medium"), Difficulty.Medium),
            (Encoding.ASCII.GetBytes("[Hard"), Difficulty.Hard),
            (Encoding.ASCII.GetBytes("[Expert"), Difficulty.Expert),
        };

        private static readonly (byte[], NoteTracks_Chart)[] NOTETRACKS =
        {
            new(Encoding.ASCII.GetBytes("Single]"),       NoteTracks_Chart.Single ),
            new(Encoding.ASCII.GetBytes("DoubleGuitar]"), NoteTracks_Chart.DoubleGuitar ),
            new(Encoding.ASCII.GetBytes("DoubleBass]"),   NoteTracks_Chart.DoubleBass ),
            new(Encoding.ASCII.GetBytes("DoubleRhythm]"), NoteTracks_Chart.DoubleRhythm ),
            new(Encoding.ASCII.GetBytes("Drums]"),        NoteTracks_Chart.Drums ),
            new(Encoding.ASCII.GetBytes("Keyboard]"),     NoteTracks_Chart.Keys ),
            new(Encoding.ASCII.GetBytes("GHLGuitar]"),    NoteTracks_Chart.GHLGuitar ),
            new(Encoding.ASCII.GetBytes("GHLBass]"),      NoteTracks_Chart.GHLBass ),
            new(Encoding.ASCII.GetBytes("GHLRhythm]"),    NoteTracks_Chart.GHLGuitar ),
            new(Encoding.ASCII.GetBytes("GHLCoop]"),      NoteTracks_Chart.GHLBass ),
        };

        private static readonly DotChartEventCombo<byte>[] EVENTS_SYNC = { TEMPO, TIMESIG, ANCHOR };
        private static readonly DotChartEventCombo<byte>[] EVENTS_EVENTS = { TEXT, };
        private static readonly DotChartEventCombo<byte>[] EVENTS_DIFF = { NOTE, SPECIAL, TEXT, };

        static YARGChartFileReader() { }

        private readonly YARGTXTReader reader;
        private readonly byte[] data;
        private readonly int length;

        private DotChartEventCombo<byte>[] eventSet = Array.Empty<DotChartEventCombo<byte>>();
        private NoteTracks_Chart _instrument;
        private Difficulty _difficulty;

        public NoteTracks_Chart Instrument => _instrument;
        public Difficulty Difficulty => _difficulty;

        public YARGChartFileReader(YARGTXTReader reader)
        {
            this.reader = reader;
            data = reader.Data;
            length = data.Length;
        }

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

        private bool ValidateTrack(ReadOnlySpan<byte> track)
        {
            if (!DoesStringMatch(track))
                return false;

            reader.GotoNextLine();
            return true;
        }

        private bool DoesStringMatch(ReadOnlySpan<byte> str)
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

            ev.Position = reader.ReadInt64();

            int start = reader.Position;
            int end = start;
            while (true)
            {
                char curr = (char) (data[end] & LOWER_CASE_MASK);
                if (curr < 'A' || 'Z' < curr)
                    break;
                ++end;
            }
            reader.Position = end;

            ReadOnlySpan<byte> span = new(data, start, end - start);
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
                while (point > position)
                {
                    char character = (char)data[point];
                    if (!ITXTReader.IsWhitespace(character) || character == '\n')
                        break;
                    --point;
                }

                if (data[point] == (byte)'\n')
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
                if (data[position + i] == (byte) '}')
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
