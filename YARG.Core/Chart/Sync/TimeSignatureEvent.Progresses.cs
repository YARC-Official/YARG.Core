using System;

namespace YARG.Core.Chart
{
    public partial class TimeSignatureChange
    {
        /// <summary>
        /// Calculates the fractional number of beats that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetBeatProgress(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) GetTicksPerBeat(sync.Resolution);
        }

        /// <summary>
        /// Calculates the whole number of beats that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetBeatCount(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / GetTicksPerBeat(sync.Resolution);
        }

        /// <summary>
        /// Calculates the percent of a beat that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetBeatPercentage(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            uint tickRate = GetTicksPerBeat(sync.Resolution);
            return (tick % tickRate) / (double) tickRate;
        }

        /// <summary>
        /// Calculates the fractional number of quarter notes that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNoteProgress(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) GetTicksPerQuarterNote(sync.Resolution);
        }

        /// <summary>
        /// Calculates the whole number of quarter notes that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetQuarterNoteCount(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / GetTicksPerQuarterNote(sync.Resolution);
        }

        /// <summary>
        /// Calculates the percent of a quarter note that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNotePercentage(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            uint tickRate = GetTicksPerQuarterNote(sync.Resolution);
            return (tick % tickRate) / (double) tickRate;
        }

        /// <summary>
        /// Calculates the fractional number of measures that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetMeasureProgress(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / (double) GetTicksPerMeasure(sync.Resolution);
        }

        /// <summary>
        /// Calculates the whole number of measures that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetMeasureCount(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            return (tick - Tick) / GetTicksPerMeasure(sync.Resolution);
        }

        /// <summary>
        /// Calculates the percent of a measure that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetMeasurePercentage(uint tick, SyncTrack sync)
        {
            CheckQuarterTick(tick, "tick");
            uint tickRate = GetTicksPerMeasure(sync.Resolution);
            return (tick % tickRate) / (double) tickRate;
        }

    }
}