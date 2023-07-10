using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart.Events.SyncTrack
{
    public class TimeSignatureEvent : SyncTrackEvent
    {
        public uint Numerator   { get; }
        public uint Denominator { get; }

        public TimeSignatureEvent(uint numerator, uint denominator, double time, uint tick) : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
    }
}