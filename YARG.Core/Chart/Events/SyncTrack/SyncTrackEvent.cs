namespace YARG.Core.Chart.Events.SyncTrack
{
    public abstract class SyncTrackEvent : ChartEvent
    {
        public SyncTrackEvent(double time, uint tick) : base(time, 0, tick, 0)
        {
        }
    }
}