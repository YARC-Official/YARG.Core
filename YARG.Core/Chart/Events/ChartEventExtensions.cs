using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static uint GetFirstTick<TEvent>(this IList<TEvent> list)
            where TEvent : ChartEvent
        {
            if (list.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = list[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this IList<TEvent> list)
            where TEvent : ChartEvent
        {
            if (list.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = list[^1];
            return chartEvent.TickEnd;
        }

        public static TEvent GetPrevious<TEvent>(this IList<TEvent> chartEvents, uint tick)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfPrevious(chartEvents, tick);
            if (index < 0)
                return null;

            return null;
        }

        public static int GetIndexOfPrevious<TEvent>(this IList<TEvent> chartEvents, uint tick)
            where TEvent : ChartEvent
        {
            int closestIndex = chartEvents.FindClosestEvent(tick);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs before (or at) the given tick
            if (chartEvents[closestIndex].Tick <= tick)
                return closestIndex;
            else
                return closestIndex - 1;
        }

        public static int FindClosestEvent<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.BinarySearchIndex(tick, Compare);

            static int Compare(TEvent currentEvent, uint targetTick)
            {
                if (currentEvent.Tick == targetTick)
                    return 0;
                else if (currentEvent.Tick < targetTick)
                    return -1;
                else
                    return 1;
            }
        }
    }
}