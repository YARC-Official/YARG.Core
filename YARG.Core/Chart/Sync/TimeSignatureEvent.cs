using System;

namespace YARG.Core.Chart
{
    public class TimeSignatureChange : SyncEvent, ICloneable<TimeSignatureChange>
    {
        public uint Numerator   { get; }
        public uint Denominator { get; }

        public TimeSignatureChange(uint numerator, uint denominator, double time, uint tick) : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public TimeSignatureChange Clone()
        {
            return new(Numerator, Denominator, Time, Tick);
        }

        /// <summary>
        /// Calculates the number of ticks per beat for this time signature.
        /// </summary>
        public uint GetTicksPerBeat(SyncTrack sync)
        {
            const float QuarterNoteDenominator = 4f;
            return (uint) (sync.Resolution * (QuarterNoteDenominator / Denominator));
        }

        /// <summary>
        /// Calculates the number of ticks per measure for this time signature.
        /// </summary>
        public uint GetTicksPerMeasure(SyncTrack sync)
        {
            return GetTicksPerBeat(sync) * Numerator;
        }

        /// <summary>
        /// Calculates the number of seconds per beat for this time signature.
        /// </summary>
        public double GetSecondsPerBeat(SyncTrack sync, TempoChange tempo)
        {
            uint ticksPerBeat = GetTicksPerBeat(sync);
            return SyncTrack.TickRangeToTimeDelta(0, ticksPerBeat, sync.Resolution, tempo);
        }

        /// <summary>
        /// Calculates the number of seconds per measure for this time signature.
        /// </summary>
        public double GetSecondsPerMeasure(SyncTrack sync, TempoChange tempo)
        {
            uint ticksPerMeasure = GetTicksPerMeasure(sync);
            return SyncTrack.TickRangeToTimeDelta(0, ticksPerMeasure, sync.Resolution, tempo);
        }

        /// <summary>
        /// Calculates the number of beats and percentage into a beat that the given tick lies at.
        /// </summary>
        public (uint count, float percent) GetBeatProgress(uint tick, SyncTrack sync)
        {
            if (tick < Tick)
                throw new ArgumentOutOfRangeException($"The given tick ({tick}) must be greater than this time signature's tick ({Tick})!");

            uint ticksPerBeat = GetTicksPerBeat(sync);
            uint count = (tick - Tick) / ticksPerBeat;
            float percent = (tick % ticksPerBeat) / (float) ticksPerBeat;
            return (count, percent);
        }

        /// <summary>
        /// Calculates the number of measures and percentage into a measure that the given tick lies at.
        /// </summary>
        public (uint count, float percent) GetMeasureProgress(uint tick, SyncTrack sync)
        {
            if (tick < Tick)
                throw new ArgumentOutOfRangeException($"The given tick ({tick}) must be greater than this time signature's tick ({Tick})!");

            uint ticksPerMeasure = GetTicksPerMeasure(sync);
            uint count = (tick - Tick) / ticksPerMeasure;
            float percent = (tick % ticksPerMeasure) / (float) ticksPerMeasure;
            return (count, percent);
        }

        /// <summary>
        /// Calculates the number of beats and percentage into a beat that the given time lies at.
        /// </summary>
        public (uint count, float percent) GetBeatProgress(double time, SyncTrack sync, TempoChange tempo)
        {
            if (time < Time)
                throw new ArgumentOutOfRangeException($"The given time ({time}) must be greater than this time signature's time ({Time})!");

            double secsPerBeat = GetSecondsPerBeat(sync, tempo);
            uint count = (uint) ((time - Time) / secsPerBeat);
            float percent = (float) ((time % secsPerBeat) / secsPerBeat);
            return (count, percent);
        }

        /// <summary>
        /// Calculates the number of measures and percentage into a measure that the given time lies at.
        /// </summary>
        public (uint count, float percent) GetMeasureProgress(double time, SyncTrack sync, TempoChange tempo)
        {
            if (time < Time)
                throw new ArgumentOutOfRangeException($"The given time ({time}) must be greater than this time signature's time ({Time})!");

            double secsPerMeasure = GetSecondsPerMeasure(sync, tempo);
            uint count = (uint) ((time - Time) / secsPerMeasure);
            float percent = (float) ((time % secsPerMeasure) / secsPerMeasure);
            return (count, percent);
        }
    }
}