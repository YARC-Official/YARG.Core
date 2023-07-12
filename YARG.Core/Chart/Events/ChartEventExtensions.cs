using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static uint GetFirstTick<TEvent>(this List<TEvent> list)
            where TEvent : ChartEvent
        {
            // Chart events are sorted
            var chartEvent = list[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this List<TEvent> list)
            where TEvent : ChartEvent
        {
            // Chart events are sorted
            var chartEvent = list[^1];
            return chartEvent.TickEnd;
        }
    }
}