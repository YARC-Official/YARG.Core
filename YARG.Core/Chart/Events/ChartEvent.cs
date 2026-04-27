using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A general event that occurs in a chart: notes, phrases, text events, etc.
    /// </summary>
    public abstract class ChartEvent
    {
        public double Time       { get; set; }
        public double TimeLength { get; set; }
        public double TimeEnd    => Time + TimeLength;

        public uint Tick       { get; set; }
        public uint TickLength { get; set; }
        public uint TickEnd    => Tick + TickLength;

        public ChartEvent(double time, double timeLength, uint tick, uint tickLength)
        {
            Time = time;
            TimeLength = timeLength;
            Tick = tick;
            TickLength = tickLength;
        }

        public ChartEvent(ChartEvent other)
            : this(other.Time, other.TimeLength, other.Tick, other.TickLength)
        {
        }

        protected bool Equals(ChartEvent other)
        {
            return Time == other.Time &&
                TimeLength == other.TimeLength &&
                Tick == other.Tick &&
                TickLength == other.TickLength;
        }

        /// <summary>
        /// Walks the list, comparing each event against the last kept event.
        /// Skips events where shouldSkip returns true.
        /// </summary>
        public static List<T> FilterByPrevious<T>(List<T> events, Func<T, T, bool> shouldSkip) where T : ChartEvent
        {
            if (events.Count == 0)
            {
                return events;
            }

            var filtered = new List<T>(events.Count) { events[0] };
            for (var i = 1; i < events.Count; i++)
            {
                if (!shouldSkip(events[i], filtered[^1]))
                {
                    filtered.Add(events[i]);
                }
            }
            return filtered;
        }

        /// <summary>
        /// Reduces events by enforcing a minimum interval between them,
        /// and optionally dropping duplicates detected by the given predicate.
        /// </summary>
        public static List<T> ReduceByInterval<T>(List<T> events, double interval,
            Func<T, T, bool> isDuplicate = null) where T : ChartEvent
        {
            return FilterByPrevious(events, (curr, prev) =>
                curr.Time - prev.Time < interval ||
                (isDuplicate != null && isDuplicate(curr, prev)));
        }

    }
}