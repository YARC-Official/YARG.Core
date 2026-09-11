using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Core.Extensions
{
    public static class ListExtensions
    {
        /// <summary>
        /// Splices two lists together, taking everything at or before time from list, everything after time from other
        /// </summary>
        /// <param name="list"></param>
        /// <param name="other"></param>
        /// <param name="time"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> Splice<T>(this List<T> list, List<T> other, double time) where T : ChartEvent
        {
            var newList = new List<T>();
            foreach (var original in list)
            {
                if (original.Time > time)
                {
                    break;
                }

                newList.Add(original);
            }

            foreach (var newItem in other)
            {
                if (newItem.Time > time)
                {
                    newList.Add(newItem);
                }
            }

            return newList;
        }

        public static List<TNote> SpliceNotes<TNote>(this List<TNote> list, List<TNote> other, double time)
            where TNote : Note<TNote>
        {
            var newList = new List<TNote>();

            foreach (var original in list)
            {
                if (original.Time > time)
                {
                    break;
                }

                newList.Add(original);
            }

            foreach (var newItem in other)
            {
                if (newItem.Time > time)
                {
                    newList.Add(newItem);
                }
            }

            if (newList.Count > 0)
            {
                newList[0].PreviousNote = null;
            }

            for (var i = 1; i < newList.Count - 1; i++)
            {
                newList[i].NextNote = newList[i + 1];
                newList[i + 1].PreviousNote = newList[i];
            }

            newList[^1].NextNote = null;

            return newList;
        }

        public static List<SoloSection> Splice(this List<SoloSection> list, List<SoloSection> other, uint tick)
        {
            var newList = new List<SoloSection>();

            foreach (var original in list)
            {
                // Handle all that are definitely in the past
                if (original.EndTick < tick)
                {
                    newList.Add(original);
                }
                else if (original.StartTick <= tick && original.EndTick >= tick)
                {
                    // Is currently active, so keep it
                    newList.Add(original);
                }
                else
                {
                    // List is sorted by time, so if we got here, we are past any current solo
                    break;
                }
            }

            foreach (var solo in other)
            {
                if (solo.StartTick > tick)
                {
                    newList.Add(solo);
                }
            }

            return newList;
        }

        public static List<CodaSection> Splice(this List<CodaSection> list, List<CodaSection> other, double time)
        {
            var newList = new List<CodaSection>();

            foreach (var original in list)
            {
                // Handle all that are definitely in the past
                if (original.EndTime < time)
                {
                    newList.Add(original);
                }
                else if (original.StartTime <= time && original.EndTime >= time)
                {
                    // Is currently active, so keep it
                    newList.Add(original);
                }
                else
                {
                    // List is sorted by time, so if we got here, we are past anything that could be current
                    break;
                }
            }

            foreach (var solo in other)
            {
                if (solo.StartTime > time)
                {
                    newList.Add(solo);
                }
            }

            return newList;
        }
    }
}