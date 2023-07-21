using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class SyncTrack
    {
        /// <returns>
        /// The power of two to multiply the denominator by to increase the beatline rate.
        /// </returns>
        public delegate uint GetBeatlineRatePower(TimeSignatureChange timeSignature);
        public delegate BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount);

        public uint Resolution { get; } = 480;
        public List<TempoChange> Tempos { get; } = new();
        public List<TimeSignatureChange> TimeSignatures { get; } = new();
        public List<Beatline> Beatlines { get; } = new();

        public SyncTrack() { }

        public SyncTrack(uint resolution, List<TempoChange> tempos, List<TimeSignatureChange> timeSignatures,
            List<Beatline> beatlines)
        {
            Resolution = resolution;
            Tempos = tempos;
            TimeSignatures = timeSignatures;
            Beatlines = beatlines;

            Tempos.Sort((x, y) => x.Tick.CompareTo(y.Tick));
            TimeSignatures.Sort((x, y) => x.Tick.CompareTo(y.Tick));
        }

        /// <summary>
        /// Generates beatlines based on the tempo map.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="endTime">
        /// The time to generate beatlines up to.
        /// </param>
        public List<Beatline> GenerateBeatlines(double endTime)
        {
            return GenerateBeatlines(TimeToTick(endTime));
        }

        /// <summary>
        /// Generates beatlines based on the tempo map.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="lastTick">
        /// The tick to generate beatlines up to, inclusive.
        /// </param>
        public List<Beatline> GenerateBeatlines(uint lastTick)
        {
            return GenerateBeatlines(lastTick, GetBeatlinePower, GetBeatlineType);

            static uint GetBeatlinePower(TimeSignatureChange currentTimeSig)
            {
                return 0;
            }

            static BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount)
            {
                // The denominator at which to generate strong beats
                const uint strongStep = 4;

                // Measure lines
                if (beatlineCount % currentTimeSig.Numerator == 0 &&
                    // 1/x time signatures only have their first beatline marked as a measure
                    (currentTimeSig.Numerator != 1 || beatlineCount < 1))
                {
                    return BeatlineType.Measure;
                }

                // All other beats
                uint strongRate = currentTimeSig.Denominator / strongStep;
                if (strongRate < 2)
                {
                    // Denominator is less than strong step, 
                    return BeatlineType.Strong;
                }

                return (beatlineCount % strongRate) == 0 ? BeatlineType.Strong : BeatlineType.Weak;
            }
        }

        /// <summary>
        /// Generates beatlines based on the tempo map and provided configuration delegates.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="endTime">
        /// The time to generate beatlines up to.
        /// </param>
        public List<Beatline> GenerateBeatlines(double endTime, GetBeatlineRatePower getBeatlinePower, GetBeatlineType getBeatlineType)
        {
            return GenerateBeatlines(TimeToTick(endTime), getBeatlinePower, getBeatlineType);
        }

        /// <summary>
        /// Generates beatlines based on the tempo map and provided configuration delegates.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <param name="lastTick">
        /// The tick to generate beatlines up to, inclusive.
        /// </param>
        public List<Beatline> GenerateBeatlines(uint lastTick, GetBeatlineRatePower getBeatlinePower, GetBeatlineType getBeatlineType)
        {
            lastTick++;
            var beatlines = new List<Beatline>((int) (lastTick / Resolution));

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

                // Generate beatlines for this time signature
                GenerateBeatsForTimeSignature(currentTimeSig, startTick, endTick);
                currentTimeSig = nextTimeSig;
            }

            // Final time signature
            GenerateBeatsForTimeSignature(currentTimeSig, currentTimeSig.Tick, lastTick);

            beatlines.TrimExcess();
            return beatlines;

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
                    beatlines.Add(new Beatline(beatlineType, time, currentTick));
                    beatlineCount++;
                    currentTick += beatlineTickRate;
                }
            }
        }

        public double TickToTime(uint tick)
        {
            // Find the current tempo marker at the given tick
            var currentTempo = Tempos.GetPrevious(tick);
            return TickToTime(tick, currentTempo);

            // Fun little tidbit: if you're between two tempo markers, you can just lerp
            // This doesn't work for the final tempo marker however, there you'll need
            // to calculate the change in time differently
            // return YargMath.Lerp(currentTempo.Time, nextTempo.Time, currentTempo.Tick, nextTempo.Tick, tick);
        }

        public uint TimeToTick(double time)
        {
            if (time < 0)
                return 0;

            // Find the current tempo marker at the given time
            var currentTempo = Tempos.GetPrevious(time);
            return TimeToTick(time, currentTempo);
        }

        public double TickToTime(uint tick, TempoChange currentTempo)
        {
            return currentTempo.Time + TickRangeToTimeDelta(currentTempo.Tick, tick, currentTempo);
        }

        public uint TimeToTick(double time, TempoChange currentTempo)
        {
            return currentTempo.Tick + TimeRangeToTickDelta(currentTempo.Time, time, currentTempo);
        }

        public double TickRangeToTimeDelta(uint tickStart, uint tickEnd, TempoChange currentTempo)
        {
            return TickRangeToTimeDelta(tickStart, tickEnd, Resolution, currentTempo);
        }

        public uint TimeRangeToTickDelta(double timeStart, double timeEnd, TempoChange currentTempo)
        {
            return TimeRangeToTickDelta(timeStart, timeEnd, Resolution, currentTempo);
        }

        public static double TickRangeToTimeDelta(uint tickStart, uint tickEnd, uint resolution,
            TempoChange currentTempo)
        {
            if (tickStart < currentTempo.Tick)
                throw new ArgumentOutOfRangeException(nameof(tickStart), tickStart,
                    $"The given start tick must occur during the given tempo (starting at {currentTempo.Tick})!");

            if (tickEnd < tickStart)
                throw new ArgumentOutOfRangeException(nameof(tickEnd), tickEnd,
                    $"The given end tick must occur after the starting tick ({tickStart})!");

            uint tickDelta = tickEnd - tickStart;
            double beatDelta = tickDelta / (double)resolution;
            double timeDelta = beatDelta * currentTempo.SecondsPerBeat;

            return timeDelta;
        }

        public static uint TimeRangeToTickDelta(double timeStart, double timeEnd, uint resolution,
            TempoChange currentTempo)
        {
            if (timeStart < currentTempo.Time)
                throw new ArgumentOutOfRangeException(nameof(timeStart), timeStart,
                    $"The given start time must occur during the given tempo (starting at {currentTempo.Tick})!");

            if (timeEnd < timeStart)
                throw new ArgumentOutOfRangeException(nameof(timeEnd), timeEnd,
                    $"The given end time must occur after the starting time ({timeStart})!");

            double timeDelta = timeEnd - timeStart;
            double beatDelta = timeDelta / currentTempo.SecondsPerBeat;
            uint tickDelta = (uint) (beatDelta * resolution);

            return tickDelta;
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
            uint totalFirstTick = 0;

            totalFirstTick = Math.Min(Tempos.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(TimeSignatures.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Tempos.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TimeSignatures.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}