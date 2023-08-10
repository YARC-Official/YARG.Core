using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public enum ChartEvent
    {
        BPM,
        TIME_SIG,
        ANCHOR,
        EVENT,
        NOTE,
        SPECIAL,
        UNKNOWN = 255,
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

    public sealed class YARGChartFileReader
    {
        internal struct EventCombo
        {
            public byte[] descriptor;
            public ChartEvent eventType;
            public EventCombo(byte[] bytes, ChartEvent chartEvent)
            {
                descriptor = bytes;
                eventType = chartEvent;
            }
        }

        internal static readonly byte[] HEADERTRACK = Encoding.ASCII.GetBytes("[Song]");
        internal static readonly byte[] SYNCTRACK =   Encoding.ASCII.GetBytes("[SyncTrack]");
        internal static readonly byte[] EVENTTRACK =  Encoding.ASCII.GetBytes("[Events]");
        internal static readonly EventCombo TEMPO =   new(Encoding.ASCII.GetBytes("B"), ChartEvent.BPM);
        internal static readonly EventCombo TIMESIG = new(Encoding.ASCII.GetBytes("TS"), ChartEvent.TIME_SIG);
        internal static readonly EventCombo ANCHOR =  new(Encoding.ASCII.GetBytes("A"), ChartEvent.ANCHOR);
        internal static readonly EventCombo TEXT =    new(Encoding.ASCII.GetBytes("E"), ChartEvent.EVENT);
        internal static readonly EventCombo NOTE =    new(Encoding.ASCII.GetBytes("N"), ChartEvent.NOTE);
        internal static readonly EventCombo SPECIAL = new(Encoding.ASCII.GetBytes("S"), ChartEvent.SPECIAL);

        internal static readonly byte[][] DIFFICULTIES =
        {
            Encoding.ASCII.GetBytes("[Easy"),
            Encoding.ASCII.GetBytes("[Medium"),
            Encoding.ASCII.GetBytes("[Hard"),
            Encoding.ASCII.GetBytes("[Expert")
        };

        internal static readonly (byte[], NoteTracks_Chart)[] NOTETRACKS =
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

        internal static readonly EventCombo[] EVENTS_SYNC = { TEMPO, TIMESIG, ANCHOR };
        internal static readonly EventCombo[] EVENTS_EVENTS = { TEXT, };
        internal static readonly EventCombo[] EVENTS_DIFF = { NOTE, SPECIAL, TEXT, };

        static YARGChartFileReader() { }

        internal const double TEMPO_FACTOR = 60000000000;

        private readonly YARGTXTReader reader;
        private readonly byte[] data;
        private readonly int length;

        private EventCombo[] eventSet = Array.Empty<EventCombo>();
        private long tickPosition = 0;
        public NoteTracks_Chart Instrument { get; private set; }
        public int Difficulty { get; private set; }

        public YARGChartFileReader(YARGTXTReader reader)
        {
            this.reader = reader;
            data = reader.Data;
            length = data.Length;
        }

        public YARGChartFileReader(byte[] data) : this(new YARGTXTReader(data)) { }

        public YARGChartFileReader(string path) : this(new YARGTXTReader(path)) { }

        public bool IsStartOfTrack()
        {
            return !reader.IsEndOfFile() && reader.PeekByte() == '[';
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
                    Difficulty = diff;
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
                    Instrument = track.Item2;
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
            tickPosition = 0;
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

        public (long, ChartEvent) ParseEvent()
        {
            int start, length;
            bool EqualSequences(byte[] descriptor)
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
            return new(position, ChartEvent.UNKNOWN);
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
                while (point > position && YARGTXTReader_Base.IsWhitespace(data[point]) && data[point] != '\n')
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
