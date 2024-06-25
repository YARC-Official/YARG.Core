using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public class WaitCountdown : ChartEvent
    {
        public const float MIN_SECONDS = 9;
        public const uint MIN_MEASURES = 4;
        public const float MIN_UPDATE_SECONDS = 1;
        private const float MIN_GET_READY_SECONDS = 2;
        public const int GET_READY_MEASURE = 2;
        public const int END_COUNTDOWN_MEASURE = 1;

        public int TotalMeasures => _measureBeatlines.Count;

        public readonly double GetReadyTime;

        private List<Beatline> _measureBeatlines;

        public int MeasuresLeft {get; private set; }

        public WaitCountdown(List<Beatline> measureBeatlines)
        {
            _measureBeatlines = measureBeatlines;

            var firstVisibleCountdownMeasure = measureBeatlines[0];
            var lastVisibleCountdownMeasure = measureBeatlines[^(END_COUNTDOWN_MEASURE + 1)];

            Time = firstVisibleCountdownMeasure.Time;
            Tick = firstVisibleCountdownMeasure.Tick;
            TimeLength = lastVisibleCountdownMeasure.Time - Time;
            TickLength = lastVisibleCountdownMeasure.Tick - Tick;

            double getReadyTotalSeconds = TimeEnd - measureBeatlines[^(GET_READY_MEASURE + 1)].Time;

            GetReadyTime = TimeEnd - Math.Max(getReadyTotalSeconds, MIN_GET_READY_SECONDS);

            MeasuresLeft = TotalMeasures;
        }

        public int CalculateMeasuresLeft(uint currentTick)
        {
            int newMeasuresLeft;
            if (currentTick >= TickEnd)
            {
                newMeasuresLeft = 0;
            }
            else if (currentTick < Tick)
            {
                newMeasuresLeft = TotalMeasures;
            }
            else
            {
                newMeasuresLeft = TotalMeasures - _measureBeatlines.GetIndexOfNext(currentTick);
            }

            MeasuresLeft = newMeasuresLeft;

            return newMeasuresLeft;
        }

        public double GetNextUpdateTime()
        {
            int nextMeasureIndex = TotalMeasures - MeasuresLeft;
            double nextUpdateTime = _measureBeatlines[nextMeasureIndex].Time;

            return Math.Min(nextUpdateTime, TimeEnd);
        }
    }
}