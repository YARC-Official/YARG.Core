using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace YARG.Core.Chart
{
    public class WaitCountdown : ChartEvent
    {
        public const double MIN_SECONDS = 10;
        public const uint MIN_MEASURES = 4;
        public const uint GET_READY_MEASURE = 2;
        public const uint END_COUNTDOWN_MEASURE = 1;
        private const double MIN_GET_READY_SECONDS = 2;

        public readonly uint TotalMeasures;

        public readonly double GetReadyTime;

        private List<CountdownTimeSig> _measuresByTimeSignature;

        private uint _measuresLeft;

        public static uint NormalizeMeasures(uint providedMeasures, TimeSignatureChange sig)
        {
            return (uint) (providedMeasures / GetMeasureNormalizeMultiplier(sig));
        }

        public static uint GetPerNormalizedMeasure(uint providedTicks, TimeSignatureChange sig)
        {
            return (uint) (providedTicks / GetMeasureNormalizeMultiplier(sig));
        }

        public static double GetPerNormalizedMeasure(double providedSeconds, TimeSignatureChange sig)
        {
            return providedSeconds / GetMeasureNormalizeMultiplier(sig);
        }

        private static double GetMeasureNormalizeMultiplier(TimeSignatureChange sig)
        {
            double multiplier = 1;

            // e.g. Normalize 2/4 and 1/4 to 4/4
            if (sig.Numerator <= sig.Denominator / 2)
            {
                multiplier = sig.Denominator / sig.Numerator;
            }

            return multiplier;
        }

        public WaitCountdown(List<CountdownTimeSig> measuresByTimeSignature, uint totalMeasures=0)
        {
            _measuresByTimeSignature = measuresByTimeSignature;

            var lastTimeSignature = measuresByTimeSignature.Last();

            Time = measuresByTimeSignature[0].Time;
            // Length properties should reflect the time the countdown spends onscreen, not the total time spent waiting
            double timeEnd = lastTimeSignature.Time + lastTimeSignature.SecondsPerMeasure * (lastTimeSignature.TotalMeasures - END_COUNTDOWN_MEASURE);
            TimeLength = timeEnd - Time;

            Tick = measuresByTimeSignature[0].Tick;
            uint tickEnd = lastTimeSignature.Tick + lastTimeSignature.TicksPerMeasure * (lastTimeSignature.TotalMeasures - END_COUNTDOWN_MEASURE);
            TickLength = tickEnd - Tick;

            if (totalMeasures == 0)
            {
                TotalMeasures = (uint) _measuresByTimeSignature.Sum(a => a.TotalMeasures);
            }
            else
            {
                TotalMeasures = totalMeasures;
            }

            _measuresLeft = TotalMeasures;

            double getReadyTotalSeconds = 0;
            uint measuresRemoved = 0;
            int i = _measuresByTimeSignature.Count - 1;
            while (measuresRemoved < GET_READY_MEASURE)
            {
                var currentStoredSig = _measuresByTimeSignature[i];
                
                uint maxMeasuresToRemove = GET_READY_MEASURE - measuresRemoved;
                uint currentMeasuresToRemove;
                if (currentStoredSig.TotalMeasures < maxMeasuresToRemove)
                {
                    currentMeasuresToRemove = maxMeasuresToRemove - currentStoredSig.TotalMeasures;
                }
                else
                {
                    currentMeasuresToRemove = maxMeasuresToRemove;
                }

                getReadyTotalSeconds += currentMeasuresToRemove * currentStoredSig.SecondsPerMeasure;

                measuresRemoved += currentMeasuresToRemove;
                i--;
            }

            GetReadyTime = TimeEnd - Math.Max(getReadyTotalSeconds, MIN_GET_READY_SECONDS);
        }

        public uint GetRemainingMeasures(uint currentTick)
        {
            if (currentTick >= TickEnd)
            {
                return 0;
            }
            
            uint measuresLeft = TotalMeasures;
            int totalTimeSignatures = _measuresByTimeSignature.Count;

            for (int i = 0; i < totalTimeSignatures; i++)
            {
                var currentTimeSigReference = _measuresByTimeSignature[i];

                if (currentTick < currentTimeSigReference.Tick)
                {
                    break;
                }
                else if (i < totalTimeSignatures-1 && currentTick > _measuresByTimeSignature[i+1].Tick)
                {
                    measuresLeft -= currentTimeSigReference.TotalMeasures;
                    continue;
                }

                uint measuresElapsedThisSig = (currentTick - currentTimeSigReference.Tick) / currentTimeSigReference.TicksPerMeasure;
                measuresLeft -= measuresElapsedThisSig;
            }

            _measuresLeft = measuresLeft;

            return measuresLeft;
        }

        public double GetNextUpdateTime()
        {
            uint nextMeasureIndex = TotalMeasures - _measuresLeft + 1;
            double nextUpdateTime = TimeEnd;
            
            // Countdown will be static during the "Get Ready!" window
            if (nextMeasureIndex <= TotalMeasures - GET_READY_MEASURE)
            {
                uint runningMeasureTotal = 0;

                for (int i = 0; i < _measuresByTimeSignature.Count; i++)
                {
                    var currentTimeSigReference = _measuresByTimeSignature[i];
                    if (nextMeasureIndex > currentTimeSigReference.TotalMeasures + runningMeasureTotal)
                    {
                        runningMeasureTotal += currentTimeSigReference.TotalMeasures;
                    }
                    else
                    {
                        nextUpdateTime = currentTimeSigReference.Time + (currentTimeSigReference.SecondsPerMeasure * (nextMeasureIndex - runningMeasureTotal));
                        break;
                    }
                }
            }

            return nextUpdateTime;
        }
    }

    public struct CountdownTimeSig
    {
        public readonly uint Tick;
        public readonly double Time;
        public readonly uint TotalMeasures;
        public readonly uint TicksPerMeasure;
        public readonly double SecondsPerMeasure;

        public CountdownTimeSig(uint tick, double time, uint totalMeasures, uint ticksPerMeasure, double secondsPerMeasure)
        {
            Tick = tick;
            Time = time;
            TotalMeasures = totalMeasures;
            TicksPerMeasure = ticksPerMeasure;
            SecondsPerMeasure = secondsPerMeasure;
        }
    }
}