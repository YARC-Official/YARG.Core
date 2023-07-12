using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class SyncTrack
    {
        public uint Resolution { get; } = 480;
        public List<TempoChange> Tempos { get; } = new();
        public List<TimeSignatureChange> TimeSignatures { get; } = new();

        public SyncTrack() { }

        public SyncTrack(uint resolution, List<TempoChange> tempos, List<TimeSignatureChange> timeSignatures)
        {
            Resolution = resolution;
            Tempos = tempos;
            TimeSignatures = timeSignatures;
        }

        public List<Beatline> GenerateBeatlines()
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
                uint beatlineTickRate = (Resolution * 4) / currentTimeSig.Denominator;

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

                    // Determine beatline type
                    BeatlineType beatlineType;
                    if ((beatlineCount % currentTimeSig.Numerator) == 0)
                    {
                        beatlineType = BeatlineType.Measure;
                    }
                    else
                    {
                        beatlineType = BeatlineType.Strong;
                    }

                    // TODO:
                    // - In-between lines for denominators <= 4?
                    // - Do we still want the behavior that the old code has for 1/x time signatures?

                    // Create beatline
                    double time = TickToTime(currentTick, currentTempo);
                    beatlines.Add(new Beatline(beatlineType, time, currentTick));
                    beatlineCount++;
                    currentTick += beatlineTickRate;
                }
                while (currentTick < endTick);
            }
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