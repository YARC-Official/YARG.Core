using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
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

    public readonly struct DotChartEventCombo<T>
        where T : unmanaged, IEquatable<T>
    {
        public readonly T[] descriptor;
        public readonly ChartEventType eventType;
        public DotChartEventCombo(T[] descriptor, ChartEventType eventType)
        {
            this.descriptor = descriptor;
            this.eventType = eventType;
        }

        public bool DoesEventMatch(ReadOnlySpan<T> span)
        {
            if (span.Length != descriptor.Length)
                return false;

            for (int i = 0; i < descriptor.Length; i++)
                if (!span[i].Equals(descriptor[i]))
                    return false;
            return true;
        }
    }

    public interface IYARGChartReader
    {
        public NoteTracks_Chart Instrument { get; }
        public Difficulty Difficulty { get; }
        public bool IsStartOfTrack();
        public bool ValidateHeaderTrack();
        public bool ValidateSyncTrack();
        public bool ValidateEventsTrack();
        public bool ValidateDifficulty();
        public bool ValidateInstrument();
        public bool IsStillCurrentTrack();
        public bool TryParseEvent(ref DotChartEvent ev);
        public void SkipEvent();
        public void NextEvent();
        public void ExtractLaneAndSustain(ref DotChartNote note);
        public void SkipTrack();
        public Dictionary<string, List<IniModifier>> ExtractModifiers(Dictionary<string, IniModifierCreator> validNodes);
    }
}
