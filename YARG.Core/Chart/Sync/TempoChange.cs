namespace YARG.Core.Chart
{
    public class TempoChange : SyncEvent
    {
        private const float SECONDS_PER_MINUTE = 60f;

        public float BeatsPerMinute { get; }
        public float SecondsPerBeat => SECONDS_PER_MINUTE / BeatsPerMinute;
        public long MilliSecondsPerBeat => (long) (SECONDS_PER_MINUTE / BeatsPerMinute * 1000);
        public long MicroSecondsPerBeat => (long) (SECONDS_PER_MINUTE / BeatsPerMinute * 1000 * 1000);

        public TempoChange(float tempo, double time, uint tick) : base(time, tick)
        {
            BeatsPerMinute = tempo;
        }
    }
}