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
        /// Generates more complex beatlines based on the tempo map.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <remarks>
        /// x/4 time signatures will have in-between beats generated.<br/>
        /// Time signatures with denominators greater than 4 will have every quarter note beat emphasized,
        /// with the rest being weak beats.<br/>
        /// 1/x time signatures will only have their first beat marked as a measure.
        /// </remarks>
        public List<Beatline> GenerateComplexBeatlines()
        {
            return GenerateBeatlines(GetBeatlinePower, GetBeatlineType);

            static uint GetBeatlinePower(TimeSignatureChange currentTimeSig)
            {
                if (currentTimeSig.Denominator == 4)
                    return 1;

                return 0;
            }

            static BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount)
            {
                // 1/x time signatures
                if (currentTimeSig.Numerator == 1)
                {
                    // Only make the first beat a measure
                    return beatlineCount < 1 ? BeatlineType.Measure : BeatlineType.Strong;
                }

                // Measure lines
                if ((beatlineCount % currentTimeSig.Numerator) == 0)
                {
                    return BeatlineType.Measure;
                }

                // In-between beats for x/4 time signatures, and
                // emphasize only every quarter note for denominators greater than 4
                if (currentTimeSig.Denominator >= 4)
                {
                    uint quarterCount = currentTimeSig.Denominator / 4;
                    if (currentTimeSig.Denominator == 4)
                        quarterCount *= 2;

                    return (beatlineCount % quarterCount) == 0 ? BeatlineType.Strong : BeatlineType.Weak;
                }

                // All other beats
                return BeatlineType.Strong;
            }
        }

        /// <summary>
        /// Generates simpler beatlines based on the tempo map.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        /// <remarks>
        /// x/4 time signatures will have in-between weak beats generated. All other time signatures will
        /// simply use measure and strong beats.
        /// </remarks>
        public List<Beatline> GenerateSimpleBeatlines()
        {
            return GenerateBeatlines(GetBeatlinePower, GetBeatlineType);

            static uint GetBeatlinePower(TimeSignatureChange currentTimeSig)
            {
                if (currentTimeSig.Denominator == 4)
                    return 1;

                return 0;
            }

            static BeatlineType GetBeatlineType(TimeSignatureChange currentTimeSig, uint beatlineCount)
            {
                // Measure lines
                if ((beatlineCount % currentTimeSig.Numerator) == 0)
                {
                    return BeatlineType.Measure;
                }

                // In-between beats for x/4 time signatures
                if (currentTimeSig.Denominator == 4 && (beatlineCount % 2) == 1)
                {
                    return BeatlineType.Weak;
                }

                // All other beats
                return BeatlineType.Strong;
            }
        }

        /// <summary>
        /// Generates beatlines based on the tempo map and provided configuration delegates.
        /// Ignores <see cref="Beatlines"/>.
        /// </summary>
        public List<Beatline> GenerateBeatlines(GetBeatlineRatePower getBeatlinePower, GetBeatlineType getBeatlineType)
        {
            uint lastTick = GetLastTick();

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
                uint endTick = nextTimeSig.Tick;

                // Generate beatlines for this time signature
                GenerateBeatsForTimeSignature(currentTimeSig, startTick, endTick);
                currentTimeSig = nextTimeSig;
            }

            // Final time signature
            GenerateBeatsForTimeSignature(currentTimeSig, currentTimeSig.Tick, lastTick);

            beatlines.TrimExcess();
            return beatlines;

            void GenerateBeatsForTimeSignature(TimeSignatureChange currentTimeSig, uint startTick, uint endTick)
            {
                uint beatlineTickFactor = currentTimeSig.Denominator * (uint)Math.Pow(2, getBeatlinePower(currentTimeSig));
                uint beatlineTickRate = (Resolution * 4) / beatlineTickFactor;

                uint beatlineCount = 0;
                uint currentTick = startTick;
                var currentTempo = Tempos[tempoIndex];
                do // Always generate at least 1 beatline on a new time signature
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

                    var beatlineType = getBeatlineType(currentTimeSig, beatlineCount);

                    // Create beatline
                    double time = TickToTime(currentTick, currentTempo);
                    beatlines.Add(new Beatline(beatlineType, time, currentTick));
                    beatlineCount++;
                    currentTick += beatlineTickRate;
                }
                while (currentTick < endTick);
            }
        }

        public TimeSignatureChange GetPrevTimeSignature(uint tick)
        {
            return TimeSignatures.GetPrevious(tick);
        }

        public TempoChange GetPrevTempoChange(uint tick)
        {
            return Tempos.GetPrevious(tick);
        }

        public double TickToTime(uint tick)
        {
            // Find the current tempo marker at the given tick
            var currentTempo = Tempos[0];
            foreach (var tempo in Tempos)
            {
                if (tempo.Tick >= tick)
                    break;

                currentTempo = tempo;
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
                return 0;

            // Find the current tempo marker at the given time
            var currentTempo = Tempos[0];
            foreach (var tempo in Tempos)
            {
                if (tempo.Time >= time)
                    break;

                currentTempo = tempo;
            }

            return TimeToTick(time, currentTempo);
        }

        public double TickToTime(uint tick, TempoChange currentTempo)
        {
            if (tick < currentTempo.Tick)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, $"The given tick must be after the given tempo's tick ({currentTempo.Tick})!");

            return currentTempo.Time + TickRangeToTimeDelta(currentTempo.Tick, tick, currentTempo.BeatsPerMinute);
        }

        public uint TimeToTick(double time, TempoChange currentTempo)
        {
            if (time < currentTempo.Time)
                throw new ArgumentOutOfRangeException(nameof(time), time, $"The given time must be after the given tempo's time ({currentTempo.Time})!");

            return currentTempo.Tick + TimeRangeToTickDelta(currentTempo.Time, time, currentTempo.BeatsPerMinute);
        }

        public double TickRangeToTimeDelta(uint tickStart, uint tickEnd, float currentBpm)
        {
            return TickRangeToTimeDelta(tickStart, tickEnd, Resolution, currentBpm);
        }

        public uint TimeRangeToTickDelta(double timeStart, double timeEnd, float currentBpm)
        {
            return TimeRangeToTickDelta(timeStart, timeEnd, Resolution, currentBpm);
        }

        public static double TickRangeToTimeDelta(uint tickStart, uint tickEnd, uint resolution, float currentBpm)
        {
            if (tickStart == tickEnd)
                return 0.0;

            const double SECONDS_PER_MINUTE = 60.0;

            uint tickDelta = tickEnd - tickStart;
            uint beatDelta = tickDelta / resolution;
            double timeDelta = beatDelta * SECONDS_PER_MINUTE / currentBpm;

            return timeDelta;
        }

        public static uint TimeRangeToTickDelta(double timeStart, double timeEnd, uint resolution, float currentBpm)
        {
            if (timeStart == timeEnd)
                return 0;

            const double SECONDS_PER_MINUTE = 60.0;

            double timeDelta = timeStart - timeEnd;
            double beatDelta = timeDelta * currentBpm / SECONDS_PER_MINUTE;
            uint tickDelta = (uint) (beatDelta * resolution);

            return tickDelta;
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