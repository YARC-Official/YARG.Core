using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class SyncTrack : ICloneable<SyncTrack>
    {
        /// <returns>
        /// The power of two to multiply the denominator by to increase the beatline rate.
        /// </returns>
        public delegate uint GetBeatlineRatePower(TimeSignatureChange timeSignature);
        public delegate BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount);

        public uint Resolution { get; }
        public uint MeasureResolution => TimeSignatureChange.GetMeasureTickResolution(Resolution);

        public List<TempoChange> Tempos { get; } = new();
        public List<TimeSignatureChange> TimeSignatures { get; } = new();
        public List<Beatline> StrongBeatlines { get; } = new();
        public List<Beatline> Beatlines { get; } = new();

        public SyncTrack(uint resolution)
        {
            Resolution = resolution;
        }

        public SyncTrack(
            uint resolution,
            List<TempoChange> tempos,
            List<TimeSignatureChange> timeSignatures,
            List<Beatline> beatlines
        )
            : this(resolution)
        {
            Tempos = tempos;
            TimeSignatures = timeSignatures;
            Beatlines = beatlines;

            // Pick out strong beatlines for beat position calculations
            StrongBeatlines.Capacity = Beatlines.Count;
            foreach (var beatline in Beatlines)
            {
                if (beatline.Type != BeatlineType.Weak)
                {
                    StrongBeatlines.Add(beatline);
                }
            }
            StrongBeatlines.TrimExcess();

            Tempos.Sort((x, y) => x.Tick.CompareTo(y.Tick));
            TimeSignatures.Sort((x, y) => x.Tick.CompareTo(y.Tick));
        }

        public SyncTrack Clone()
        {
            return new(
                Resolution,
                Tempos.Duplicate(),
                TimeSignatures.Duplicate(),
                Beatlines.Duplicate()
            );
        }

        /// <summary>
        /// Generates beatlines based on the tempo map.
        /// Overwrites <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="endTime">
        /// The time to generate beatlines up to.
        /// </param>
        /// <param name="keepExisting"></param>
        public void GenerateBeatlines(double endTime, bool keepExisting = false)
        {
            GenerateBeatlines(TimeToTick(endTime), keepExisting);
        }

        /// <summary>
        /// Generates beatlines based on the tempo map.
        /// Overwrites <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="lastTick">
        /// The tick to generate beatlines up to, inclusive.
        /// </param>
        /// <param name="keepExisting"></param>
        public void GenerateBeatlines(uint lastTick, bool keepExisting = false)
        {
            GenerateBeatlines(lastTick, GetBeatlinePower, GetBeatlineType, keepExisting);

            static uint GetBeatlinePower(TimeSignatureChange currentTimeSig)
            {
                return 0;
            }

            static BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount)
            {
                // The denominator at which to generate strong beats
                const uint strongStep = 4;

                uint measureBeatCount = beatlineCount % currentTimeSig.Numerator;
                uint strongRate = currentTimeSig.Denominator is <= 4 or < strongStep
                    ? 1
                    : currentTimeSig.Denominator / strongStep;

                // 1/x time signatures
                if (currentTimeSig.Numerator == 1)
                {
                    // Only the first beatline should be a measure line
                    if (beatlineCount < 1)
                        return BeatlineType.Measure;

                    // The rest are emphasized periodically
                    return (beatlineCount % strongRate) == 0 ? BeatlineType.Strong : BeatlineType.Weak;
                }

                // Measure lines
                if (measureBeatCount == 0)
                    return BeatlineType.Measure;

                // Only use weak beats on x/8 or greater
                if (currentTimeSig.Denominator is <= 4 or < strongStep)
                    return BeatlineType.Strong;

                // Emphasize beatlines periodically
                if ((measureBeatCount % strongRate) == 0)
                {
                    // Always force the last beatline of a measure to be weak
                    if (measureBeatCount == currentTimeSig.Numerator - 1)
                        return BeatlineType.Weak;

                    return BeatlineType.Strong;
                }
                else
                {
                    return BeatlineType.Weak;
                }
            }
        }

        /// <summary>
        /// Generates beatlines based on the tempo map and provided configuration delegates.
        /// Overwrites <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="endTime">
        /// The time to generate beatlines up to.
        /// </param>
        /// <param name="getBeatlinePower"></param>
        /// <param name="getBeatlineType"></param>
        /// <param name="keepExisting"></param>
        public void GenerateBeatlines(double endTime, GetBeatlineRatePower getBeatlinePower, GetBeatlineType getBeatlineType, bool keepExisting = false)
        {
            GenerateBeatlines(TimeToTick(endTime), getBeatlinePower, getBeatlineType, keepExisting);
        }

        /// <summary>
        /// Generates beatlines based on the tempo map and provided configuration delegates.
        /// Overwrites <see cref="Beatlines"/> unless <see cref="keepExisting"/> is set.
        /// </summary>
        /// <param name="lastTick">
        /// The tick to generate beatlines up to, inclusive.
        /// </param>
        /// <param name="getBeatlinePower"></param>
        /// <param name="getBeatlineType"></param>
        /// <param name="keepExisting"></param>
        public void GenerateBeatlines(uint lastTick, GetBeatlineRatePower getBeatlinePower, GetBeatlineType getBeatlineType, bool keepExisting = false)
        {
            lastTick++;

            uint lastBeatlineTick = 0;

            if (!keepExisting)
            {
                Beatlines.Clear();
                Beatlines.Capacity = (int) (lastTick / Resolution);
            }
            else
            {
                // Find the current last beatline
                if (Beatlines.Count > 0)
                {
                    lastBeatlineTick = Beatlines[^1].Tick;
                }

                // In case lastTick is before the current final beatline, do nothing
                if (lastTick <= lastBeatlineTick)
                {
                    return;
                }
            }


            // List indexes
            int tempoIndex = 0;
            int timeSigIndex = 0;

            // Iterate through all of the time signatures
            var currentTimeSig = TimeSignatures[timeSigIndex++];
            for (; timeSigIndex < TimeSignatures.Count; timeSigIndex++)
            {
                var nextTimeSig = TimeSignatures[timeSigIndex];

                // Determine bounds
                uint startTick = currentTimeSig.Tick;
                uint endTick = nextTimeSig.Tick - 1;

                // If startTick and endTick are both prior to the current last beatline, we don't need to do anything
                if (startTick <= lastBeatlineTick && endTick <= lastBeatlineTick)
                {
                    continue;
                }

                // If startTick is prior to the current last beatline, we need to move it to the last beatline
                startTick = Math.Max(startTick, lastBeatlineTick);

                // Generate beatlines for this time signature
                GenerateBeatsForTimeSignature(currentTimeSig, startTick, endTick);
                currentTimeSig = nextTimeSig;
            }


            uint finalStartTick = Math.Max(currentTimeSig.Tick, lastBeatlineTick);
            // Final time signature
            GenerateBeatsForTimeSignature(currentTimeSig, finalStartTick, lastTick);

            Beatlines.TrimExcess();

            // Refresh strong beatlines list
            StrongBeatlines.Clear();
            StrongBeatlines.Capacity = Beatlines.Count;
            foreach (var beatline in Beatlines)
            {
                if (beatline.Type != BeatlineType.Weak)
                {
                    StrongBeatlines.Add(beatline);
                }
            }
            StrongBeatlines.TrimExcess();

            void GenerateBeatsForTimeSignature(TimeSignatureChange timeSignature, uint startTick, uint endTick)
            {
                uint beatlineTickFactor = timeSignature.Denominator * (uint)Math.Pow(2, getBeatlinePower(timeSignature));
                uint beatlineTickRate = (Resolution * 4) / beatlineTickFactor;

                uint beatlineCount = 0;
                uint currentTick = startTick;
                var currentTempo = Tempos[tempoIndex];
                while (currentTick <= endTick)
                {
                    // Progress to current tempo
                    while (tempoIndex < Tempos.Count - 1)
                    {
                        var nextTempo = Tempos[tempoIndex + 1];
                        if (nextTempo.Tick > currentTick)
                            break;

                        currentTempo = nextTempo;
                        tempoIndex++;
                    }

                    var beatlineType = getBeatlineType(timeSignature, beatlineCount);

                    // Create beatline
                    double time = TickToTime(currentTick, currentTempo);
                    Beatlines.Add(new Beatline(beatlineType, time, currentTick));
                    beatlineCount++;
                    currentTick += beatlineTickRate;
                }
            }
        }

        public double TickToTime(uint tick)
        {
            // Find the current tempo marker at the given tick
            var currentTempo = Tempos.LowerBoundElement(tick);
            if (currentTempo is null)
            {
                return 0;
            }

            return TickToTime(tick, currentTempo);

            // Fun little tidbit: if you're between two tempo markers, you can just lerp
            // This doesn't work for the final tempo marker however, there you'll need
            // to calculate the change in time differently
            // return YargMath.Lerp(currentTempo.Time, nextTempo.Time, currentTempo.Tick, nextTempo.Tick, tick);
        }

        public uint TimeToTick(double time)
        {
            if (time < 0)
            {
                return 0;
            }

            // Find the current tempo marker at the given time
            var currentTempo = Tempos.LowerBoundElement(time);
            if (currentTempo is null)
            {
                return 0;
            }

            return TimeToTick(time, currentTempo);
        }

        public double TickToTime(uint tick, TempoChange currentTempo)
        {
            return currentTempo.TickToTime(tick, Resolution);
        }

        public uint TimeToTick(double time, TempoChange currentTempo)
        {
            return currentTempo.TimeToTick(time, Resolution);
        }

        public uint QuarterTickToMeasureTick(uint quarterTick)
        {
            int timeSigIndex = TimeSignatures.LowerBound(quarterTick);
            if (timeSigIndex < 0)
            {
                return 0;
            }

            var timeSig = TimeSignatures[timeSigIndex];

            // Interrupted time signatures need special handling for correct results
            if (timeSig.IsInterrupted)
            {
                var nextTimeSig = TimeSignatures[timeSigIndex + 1];
                return timeSig.QuarterTickToMeasureTick(quarterTick, nextTimeSig);
            }

            return timeSig.QuarterTickToMeasureTick(quarterTick, Resolution);
        }

        public uint MeasureTickToQuarterTick(uint measureTick)
        {
            int timeSigIndex = TimeSignatures.LowerBound(
                measureTick,
                (TimeSignatureChange timeSig, uint measureTick) => timeSig.MeasureTick.CompareTo(measureTick),
                before: true
            );
            if (timeSigIndex < 0)
            {
                return 0;
            }

            var timeSig = TimeSignatures[timeSigIndex];

            // Interrupted time signatures need special handling for correct results
            if (timeSig.IsInterrupted)
            {
                var nextTimeSig = TimeSignatures[timeSigIndex + 1];
                return timeSig.MeasureTickToQuarterTick(measureTick, nextTimeSig);
            }

            return timeSig.MeasureTickToQuarterTick(measureTick, Resolution);
        }

        public uint TimeToMeasureTick(double time)
        {
            uint quarterTick = TimeToTick(time);
            return QuarterTickToMeasureTick(quarterTick);
        }

        public double MeasureTickToTime(uint measureTick)
        {
            uint quarterTick = MeasureTickToQuarterTick(measureTick);
            return TickToTime(quarterTick);
        }

        public double GetStartTime()
        {
            // The sync track always starts at the very beginning of the chart
            return 0;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;

            totalEndTime = Math.Max(Tempos.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(TimeSignatures.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            // The sync track always starts at the very beginning of the chart
            return 0;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Tempos.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TimeSignatures.GetLastTick(), totalLastTick);

            return totalLastTick;
        }

        public double GetStrongBeatPosition(uint quarterTick)
        {
            int beatIndex = StrongBeatlines.LowerBound(quarterTick);
            if (beatIndex < 0)
            {
                return 0;
            }

            if (beatIndex + 1 >= StrongBeatlines.Count)
            {
                return beatIndex;
            }

            var currentBeat = StrongBeatlines[beatIndex];
            var nextBeat = StrongBeatlines[beatIndex + 1];
            return beatIndex + YargMath.InverseLerpD(currentBeat.Tick, nextBeat.Tick, quarterTick);
        }

        public double GetWeakBeatPosition(uint quarterTick)
        {
            int beatIndex = Beatlines.LowerBound(quarterTick);
            if (beatIndex < 0)
            {
                return 0;
            }

            if (beatIndex + 1 >= Beatlines.Count)
            {
                return beatIndex;
            }

            var currentBeat = Beatlines[beatIndex];
            var nextBeat = Beatlines[beatIndex + 1];
            return beatIndex + YargMath.InverseLerpD(currentBeat.Tick, nextBeat.Tick, quarterTick);
        }

        public double GetDenominatorBeatPosition(uint quarterTick)
        {
            int timeSigIndex = TimeSignatures.LowerBound(quarterTick);
            if (timeSigIndex < 0)
            {
                return 0;
            }

            var timeSig = TimeSignatures[timeSigIndex];

            // Interrupted time signatures need special handling for correct results
            if (timeSig.IsInterrupted)
            {
                var nextTimeSig = TimeSignatures[timeSigIndex + 1];
                return timeSig.DenominatorBeatCount + timeSig.GetDenominatorBeatProgress(quarterTick, nextTimeSig, Resolution);
            }

            return timeSig.DenominatorBeatCount + timeSig.GetDenominatorBeatProgress(quarterTick, Resolution);
        }

        public double GetQuarterNotePosition(uint quarterTick)
        {
            var timeSig = TimeSignatures.LowerBoundElement(quarterTick);
            if (timeSig is null)
            {
                return 0;
            }

            // Interrupted time signatures do not require special handling here,
            // they have no bearing on how long a quarter note lasts in ticks
            return timeSig.QuarterNoteCount + timeSig.GetQuarterNoteProgress(quarterTick, Resolution);
        }

        public double GetMeasurePosition(uint quarterTick)
        {
            int timeSigIndex = TimeSignatures.LowerBound(quarterTick);
            if (timeSigIndex < 0)
            {
                return 0;
            }

            var timeSig = TimeSignatures[timeSigIndex];

            // Interrupted time signatures need special handling for correct results
            if (timeSig.IsInterrupted)
            {
                var nextTimeSig = TimeSignatures[timeSigIndex + 1];
                return timeSig.MeasureCount + timeSig.GetMeasureProgress(quarterTick, nextTimeSig);
            }

            return timeSig.MeasureCount + timeSig.GetMeasureProgress(quarterTick, Resolution);
        }

        public double GetStrongBeatPosition(double time)
        {
            uint quarterTick = TimeToTick(time);
            return GetStrongBeatPosition(quarterTick);
        }

        public double GetWeakBeatPosition(double time)
        {
            uint quarterTick = TimeToTick(time);
            return GetWeakBeatPosition(quarterTick);
        }

        public double GetDenominatorBeatPosition(double time)
        {
            uint quarterTick = TimeToTick(time);
            return GetDenominatorBeatPosition(quarterTick);
        }

        public double GetQuarterNotePosition(double time)
        {
            uint quarterTick = TimeToTick(time);
            return GetQuarterNotePosition(quarterTick);
        }

        public double GetMeasurePosition(double time)
        {
            uint quarterTick = TimeToTick(time);
            return GetMeasurePosition(quarterTick);
        }
    }
}