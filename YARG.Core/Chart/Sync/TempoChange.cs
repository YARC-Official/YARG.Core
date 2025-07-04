using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Chart
{
    public class TempoChange : SyncEvent, IEquatable<TempoChange>, ICloneable<TempoChange>
    {
        private const double SECONDS_PER_MINUTE = 60;

        public double BeatsPerMinute { get; }
        public double SecondsPerBeat => SECONDS_PER_MINUTE / BeatsPerMinute;
        public long MilliSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute) / 1000;
        public long MicroSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute);

        public TempoChange(double tempo, double time, uint tick) : base(time, tick)
        {
            BeatsPerMinute = tempo;
        }

        public TempoChange Clone()
        {
            return new(BeatsPerMinute, Time, Tick);
        }

        private void CheckTime(double time, [CallerArgumentExpression(nameof(time))] string name = "")
        {
            if (time < Time)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void CheckTick(uint tick, [CallerArgumentExpression(nameof(tick))] string name = "")
        {
            if (tick < Tick)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BpmToMicroSeconds(double tempo)
        {
            double secondsPerBeat = SECONDS_PER_MINUTE / tempo;
            double microseconds = secondsPerBeat * 1000 * 1000;
            return (long) microseconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double MicroSecondsToBpm(long usecs)
        {
            double secondsPerBeat = usecs / 1000f / 1000f;
            double tempo = SECONDS_PER_MINUTE / secondsPerBeat;
            return tempo;
        }

        public double TickToTime(uint tick, uint resolution)
        {
            CheckTick(tick);

            double tickDelta = tick - Tick;
            double beatDelta = tickDelta / resolution;
            double timeDelta = beatDelta * SecondsPerBeat;

            return Time + timeDelta;
        }

        public uint TimeToTick(double time, uint resolution)
        {
            CheckTime(time);

            double timeDelta = time - Time;
            double beatDelta = timeDelta / SecondsPerBeat;
            double tickDelta = beatDelta * resolution;

            return Tick + (uint) Math.Round(tickDelta);
        }

        public static bool operator ==(TempoChange? left, TempoChange? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TempoChange? left, TempoChange? right)
            => !(left == right);

        public bool Equals(TempoChange other)
        {
            return base.Equals(other) && BeatsPerMinute == other.BeatsPerMinute;
        }

        public override bool Equals(object? obj)
            => obj is TempoChange tempo && Equals(tempo);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"Tempo {BeatsPerMinute} at tick {Tick}, time {Time}";
        }
    }
}