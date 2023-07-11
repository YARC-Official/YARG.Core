namespace YARG.Core.Chart
{
    public class TempoChange : SyncEvent
    {
        public float BeatsPerMinute { get; }
        public long MicroSecondsPerBeat => (long) (60 / BeatsPerMinute) * 1000 * 1000;

        public TempoChange(float tempo, double time, uint tick) : base(time, tick)
        {
            BeatsPerMinute = tempo;
        }
    }
}