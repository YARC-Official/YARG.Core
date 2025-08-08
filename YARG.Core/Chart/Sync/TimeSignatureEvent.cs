using System;

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

        public uint MeasureCount { get; }
        public uint DenominatorBeatCount { get; }
        public double QuarterNoteCount { get; }

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

            uint measureCount,
            uint denominatorBeatCount,
            double quarterNoteCount,

            bool interrupted = false
        )
            : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;

            MeasureCount = measureCount;
            DenominatorBeatCount = denominatorBeatCount;
            QuarterNoteCount = quarterNoteCount;

            MeasureTick = measureTick;
            IsInterrupted = interrupted;
        }

        public TimeSignatureChange Clone()
        {
            return new(
                Numerator,
                Denominator,

                Time,
                Tick,
                MeasureTick,

                MeasureCount,
                DenominatorBeatCount,
                QuarterNoteCount,

                IsInterrupted
            );
        }

        public static uint GetMeasureTickResolution(uint quarterResolution)
        {
            return quarterResolution * MEASURE_RESOLUTION_SCALE;
        }

        private void CheckQuarterTick(uint quarterTick, string name = "quarterTick")
        {
            if (quarterTick < Tick)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void CheckMeasureTick(uint measureTick, string name = "measureTick")
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
        /// <remarks>
        /// Note that this is relative to the actual denominator of the time signature,
        /// and does not necessarily line up with beatlines!
        /// </remarks>
        public uint GetTicksPerDenominatorBeat(uint quarterResolution)
        {
            return (uint) ((quarterResolution * QUARTER_NOTE_DENOMINATOR) / (double) Denominator);
        }

        /// <summary>
        /// Calculates the number of ticks per measure for this time signature.
        /// </summary>
        public uint GetTicksPerMeasure(uint quarterResolution)
        {
            return GetTicksPerDenominatorBeat(quarterResolution) * Numerator;
        }

        /// <summary>
        /// Calculates the fractional number of quarter notes that the given tick lies at,
        /// relative to this time signature.
        /// </summary>
        public double GetQuarterNoteProgress(uint tick, uint resolution)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) resolution;
        }

        /// <summary>
        /// Calculates the fractional number of denominator beats that the given tick lies at,
        /// relative to this time signature.
        /// </summary>
        public double GetDenominatorBeatProgress(uint tick, uint resolution)
        {
            CheckInterrupted();
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) GetTicksPerDenominatorBeat(resolution);
        }

        /// <summary>
        /// Calculates the fractional number of measures that the given tick lies at,
        /// relative to this time signature.
        /// </summary>
        public double GetMeasureProgress(uint tick, uint resolution)
        {
            CheckInterrupted();
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) GetTicksPerMeasure(resolution);
        }

        /// <summary>
        /// Calculates the fractional number of denominator beats that the given tick lies at,
        /// between this time signature and the next.
        /// </summary>
        public double GetDenominatorBeatProgress(uint tick, TimeSignatureChange nextTimeSig, uint resolution)
        {
            CheckQuarterTick(tick, "tick");
            CheckQuarterTick(nextTimeSig.Tick, "nextTimeSig.Tick");

            uint beatResolution = GetTicksPerDenominatorBeat(resolution);
            uint distanceToNext = nextTimeSig.Tick - tick;

            // If the last beat is shorter than it should be, interpolate to smooth the difference out
            if (distanceToNext < beatResolution)
            {
                uint lastBeatTick = nextTimeSig.Tick - beatResolution;

                double progressToLastBeat = (lastBeatTick - Tick) / (double) beatResolution;
                double lastBeatProgress = YargMath.InverseLerpD(lastBeatTick, nextTimeSig.Tick, tick);
                return progressToLastBeat + lastBeatProgress;
            }

            return (tick - Tick) / (double) beatResolution;
        }

        /// <summary>
        /// Calculates the fractional number of measures that the given tick lies at,
        /// between this time signature and the next.
        /// </summary>
        public double GetMeasureProgress(uint tick, TimeSignatureChange nextTimeSig)
        {
            CheckQuarterTick(tick, "tick");
            CheckQuarterTick(nextTimeSig.Tick, "nextTimeSig.Tick");
            return YargMath.InverseLerpD(Tick, nextTimeSig.Tick, tick);
        }

        /// <summary>
        /// Converts a quarter-note-based tick into a measure-based tick.
        /// </summary>
        public uint QuarterTickToMeasureTick(uint quarterTick, uint quarterResolution)
        {
            CheckInterrupted();
            CheckQuarterTick(quarterTick);

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
            CheckQuarterTick(quarterTick);
            CheckQuarterTick(nextTimeSig.Tick, "nextTimeSig.Tick");

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
            CheckMeasureTick(nextTimeSig.MeasureTick, "nextTimeSig.MeasureTick");

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
                Denominator == other.Denominator &&
                MeasureTick == other.MeasureTick &&
                MeasureCount == other.MeasureCount &&
                DenominatorBeatCount == other.DenominatorBeatCount &&
                QuarterNoteCount == other.QuarterNoteCount;
        }

        public override bool Equals(object? obj)
            => obj is TimeSignatureChange timeSig && Equals(timeSig);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"Time signature {Numerator}/{Denominator} at tick {Tick}, time {Time}, measure tick {MeasureTick} (measure count: {MeasureCount}, beat count: {DenominatorBeatCount}, quarter count: {QuarterNoteCount})";
        }
    }
}