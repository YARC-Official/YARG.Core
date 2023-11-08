using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Chart
{
    public class TempoChange : SyncEvent, ICloneable<TempoChange>
    {
        private const float SECONDS_PER_MINUTE = 60f;

        public float BeatsPerMinute { get; }
        public float SecondsPerBeat => SECONDS_PER_MINUTE / BeatsPerMinute;
        public long MilliSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute) / 1000;
        public long MicroSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute);

        public TempoChange(float tempo, double time, uint tick) : base(time, tick)
        {
            BeatsPerMinute = tempo;
        }

        public TempoChange Clone()
        {
            return new(BeatsPerMinute, Time, Tick);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BpmToMicroSeconds(float tempo)
        {
            double secondsPerBeat = SECONDS_PER_MINUTE / tempo;
            double microseconds = secondsPerBeat * 1000 * 1000;
            return (long) microseconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MicroSecondsToBpm(long usecs)
        {
            double secondsPerBeat = usecs / 1000f / 1000f;
            double tempo = SECONDS_PER_MINUTE / secondsPerBeat;
            return (float) tempo;
        }
    }
}