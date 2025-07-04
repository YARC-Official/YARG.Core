using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Chart
{
    public partial class TimeSignatureChange : SyncEvent, IEquatable<TimeSignatureChange>, ICloneable<TimeSignatureChange>
    {
        public const uint QUARTER_NOTE_DENOMINATOR = 4;
        public const uint MEASURE_RESOLUTION_SCALE = 4;

        public uint Numerator   { get; }
        public uint Denominator { get; }

        /// <summary>
        /// The time signature's position relative to measures.
        /// </summary>
        /// <seealso cref="GetMeasureTickResolution"/>
        public uint MeasureTick { get; }

        /// <summary>
        /// Whether this time signature is interuppted by a following misaligned time signature.
        /// </summary>
        /// <remarks>
        /// Interrupted time signatures cannot be used on their own for time calculations or conversions.
        /// They must be paired with the
        /// </remarks>
        public bool IsInterrupted { get; }

        public TimeSignatureChange(
            uint numerator,
            uint denominator,
            double time,
            uint tick,
            uint measureTick,
            bool interrupted = false
        )
            : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;

            MeasureTick = measureTick;
            IsInterrupted = interrupted;
        }

        public TimeSignatureChange Clone()
        {
            return new(Numerator, Denominator, Time, Tick, MeasureTick, IsInterrupted);
        }

        public static uint GetMeasureTickResolution(uint beatResolution)
        {
            return beatResolution * MEASURE_RESOLUTION_SCALE;
        }

        private void CheckTick(uint tick, [CallerArgumentExpression(nameof(tick))] string name = "")
        {
            if (tick < Tick)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void CheckMeasureTick(uint measureTick, [CallerArgumentExpression(nameof(measureTick))] string name = "")
        {
            if (measureTick < MeasureTick)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void CheckInterrupted()
        {
            if (IsInterrupted)
            {
                throw new InvalidOperationException("Interrupted time signatures cannot be used on their own for time calculations or conversions.");
            }
        }

        /// <summary>
        /// Calculates the number of ticks per beat for this time signature.
        /// </summary>
        public uint GetTicksPerBeat(uint beatResolution)
        {
            CheckInterrupted();
            return (uint) (beatResolution * (QUARTER_NOTE_DENOMINATOR / (double) Denominator));
        }

        /// <summary>
        /// Calculates the number of ticks per measure for this time signature.
        /// </summary>
        public uint GetTicksPerMeasure(uint beatResolution)
        {
            CheckInterrupted();
            return GetTicksPerBeat(beatResolution) * Numerator;
        }

        // For template generation purposes
        private uint GetTicksPerQuarterNote(uint beatResolution)
        {
            CheckInterrupted();
            return beatResolution;
        }

        /// <summary>
        /// Converts a quarter-note-based tick into a measure-based tick.
        /// </summary>
        public uint QuarterTickToMeasureTick(uint quarterTick, uint quarterResolution)
        {
            CheckInterrupted();
            CheckTick(quarterTick);

            uint measureResolution = GetMeasureTickResolution(quarterResolution);
            double quarterTicksPerMeasure = GetTicksPerMeasure(quarterResolution);

            double quarterTickDelta = quarterTick - Tick;
            double measureDelta = quarterTickDelta / quarterTicksPerMeasure;
            double measureTickDelta = measureDelta * measureResolution;

            return MeasureTick + (uint) Math.Round(measureTickDelta);
        }

        /// <summary>
        /// Converts a measure-based tick into a quarter-note-based tick.
        /// </summary>
        public uint MeasureTickToQuarterTick(uint measureTick, uint quarterResolution)
        {
            CheckInterrupted();
            CheckMeasureTick(measureTick);

            uint measureResolution = GetMeasureTickResolution(quarterResolution);
            double quarterTicksPerMeasure = GetTicksPerMeasure(quarterResolution);

            double measureTickDelta = measureTick - MeasureTick;
            double measureDelta = measureTickDelta / measureResolution;
            double quarterTickDelta = measureDelta * quarterTicksPerMeasure;

            return Tick + (uint) Math.Round(quarterTickDelta);
        }

        /// <summary>
        /// Converts a quarter-note-based tick into a measure-based tick,
        /// using the next measure as a reference point.
        /// </summary>
        /// <remarks>
        /// This can be used even for time signatures which are interrupted:
        /// the given tick is simply interpolated between this time signature and the given next one.
        /// </remarks>
        public uint QuarterTickToMeasureTick(uint quarterTick, TimeSignatureChange nextTimeSig)
        {
            CheckTick(quarterTick);
            CheckTick(nextTimeSig.Tick);

            double measureProgress = YargMath.InverseLerpD(Tick, nextTimeSig.Tick, quarterTick);
            return YargMath.Lerp(MeasureTick, nextTimeSig.MeasureTick, measureProgress);
        }

        /// <summary>
        /// Converts a measure-based tick into a quarter-note-based tick,
        /// using the next measure as a reference point.
        /// </summary>
        /// <summary>
        /// This can be used even for time signatures which are interrupted:
        /// the given tick is simply interpolated between this time signature and the given next one.
        /// </summary>
        public uint MeasureTickToQuarterTick(uint measureTick, TimeSignatureChange nextTimeSig)
        {
            CheckMeasureTick(measureTick);
            CheckMeasureTick(nextTimeSig.MeasureTick);

            double measureProgress = YargMath.InverseLerpD(MeasureTick, nextTimeSig.MeasureTick, measureTick);
            return YargMath.Lerp(Tick, nextTimeSig.Tick, measureProgress);
        }

        public static bool operator ==(TimeSignatureChange? left, TimeSignatureChange? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TimeSignatureChange? left, TimeSignatureChange? right)
            => !(left == right);

        public bool Equals(TimeSignatureChange other)
        {
            return base.Equals(other) &&
                Numerator == other.Numerator &&
                Denominator == other.Denominator;
        }

        public override bool Equals(object? obj)
            => obj is TimeSignatureChange timeSig && Equals(timeSig);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"Time signature {Numerator}/{Denominator} at tick {Tick}, time {Time}";
        }
    }
}