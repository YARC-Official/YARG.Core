using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static double GetStartTime<TEvent>(this IList<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Time;
        }

        public static double GetEndTime<TEvent>(this IList<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TimeEnd;
        }

        public static uint GetFirstTick<TEvent>(this IList<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this IList<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TickEnd;
        }

        public static TEvent GetPrevious<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfPrevious(events, time);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent GetPrevious<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfPrevious(events, tick);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent GetNext<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfNext(events, time);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent GetNext<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfNext(events, tick);
            if (index < 0)
                return null;

            return events[index];
        }

        public static int GetIndexOfPrevious<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(time);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs before (or at) the given time
            if (events[closestIndex].Time <= time)
                return closestIndex;
            else
                return closestIndex - 1;
        }

        public static int GetIndexOfPrevious<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(tick);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs before (or at) the given tick
            if (events[closestIndex].Tick <= tick)
                return closestIndex;
            else
                return closestIndex - 1;
        }

        public static int GetIndexOfNext<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(time);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs after the given time
            if (events[closestIndex].Time > time)
                return closestIndex;
            else if (closestIndex < events.Count - 1)
                return closestIndex + 1;

            return -1;
        }

        public static int GetIndexOfNext<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(tick);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs after the given tick
            if (events[closestIndex].Tick > tick)
                return closestIndex;
            else if (closestIndex < events.Count - 1)
                return closestIndex + 1;

            return -1;
        }

        public static TEvent FindClosestEvent<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.BinarySearch(time, Compare);

            static int Compare(TEvent currentEvent, double targetTime)
            {
                if (currentEvent.Time == targetTime)
                    return 0;
                else if (currentEvent.Time < targetTime)
                    return -1;
                else
                    return 1;
            }
        }

        public static TEvent FindClosestEvent<TEvent>(this IList<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.BinarySearch(tick, Compare);

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

        public static int FindClosestEventIndex<TEvent>(this IList<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.BinarySearchIndex(time, Compare);

            static int Compare(TEvent currentEvent, double targetTime)
            {
                if (currentEvent.Time == targetTime)
                    return 0;
                else if (currentEvent.Time < targetTime)
                    return -1;
                else
                    return 1;
            }
        }

        public static int FindClosestEventIndex<TEvent>(this IList<TEvent> events, uint tick)
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