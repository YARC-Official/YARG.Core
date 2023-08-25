using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public enum ChartEvent_FW
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

    public interface IYARGChartReader
    {
        public NoteTracks_Chart Instrument { get; }
        public int Difficulty { get; }
        public bool IsStartOfTrack();
        public bool ValidateHeaderTrack();
        public bool ValidateSyncTrack();
        public bool ValidateEventsTrack();
        public bool ValidateDifficulty();
        public bool ValidateInstrument();
        public bool IsStillCurrentTrack();
        public (long, ChartEvent_FW) ParseEvent();
        public void SkipEvent();
        public void NextEvent();
        public (int, long) ExtractLaneAndSustain();
        public void SkipTrack();
        public Dictionary<string, List<IniModifier>> ExtractModifiers(Dictionary<string, IniModifierCreator> validNodes);
    }
}
