namespace YARG.Core.Chart.Events.SyncTrack
{
    public class BpmEvent : SyncTrackEvent
    {
        public uint UnscaledValue { get; }

        public float Value { get; }

        public BpmEvent(uint unscaledValue, double time, uint tick) : base(time, tick)
        {
            UnscaledValue = unscaledValue;
            Value = unscaledValue / 1000.0f;
        }
    }
}