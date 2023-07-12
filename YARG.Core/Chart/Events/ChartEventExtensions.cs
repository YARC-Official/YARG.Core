using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static uint GetFirstTick<TEvent>(this IList<TEvent> list)
            where TEvent : ChartEvent
        {
            // Chart events are sorted
            var chartEvent = list[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this IList<TEvent> list)
            where TEvent : ChartEvent
        {
            // Chart events are sorted
            var chartEvent = list[^1];
            return chartEvent.TickEnd;
        }

        public static TEvent GetPrevious<TEvent>(this IList<TEvent> chartEvents, uint tick)
            where TEvent : ChartEvent
        {
            int pos = GetIndexOfPrevious(chartEvents, tick);
            if (pos != -1)
            {
                return chartEvents[pos];
            }

            return null;
        }

        public static int GetIndexOfPrevious<TEvent>(this IList<TEvent> chartEvents, uint position) where TEvent : ChartEvent
        {
            int closestPos = FindClosestEventToPosition(position, chartEvents);
            if (closestPos != -1)
            {
                // Select the smaller of the two
                if (chartEvents[closestPos].Tick <= position)
                {
                    return closestPos;
                }

                if (closestPos > 0)
                {
                    return closestPos - 1;
                }

                return -1;
            }

            return closestPos;
        }

        public static int FindClosestEventToPosition<TEvent>(uint position, IList<TEvent> events) where TEvent : ChartEvent
        {
            // Binary search
            int low = 0;
            int high = events.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;

                if (events[mid].Tick == position)
                {
                    return mid;
                }

                if (events[mid].Tick < position)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return -1;
        }
    }
}