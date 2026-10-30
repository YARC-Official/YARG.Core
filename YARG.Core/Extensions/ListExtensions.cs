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

            for (var i = 0; i < newList.Count - 1; i++)
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

        public static void UpdateNoteCount<TNote>(this List<EngineManager.UnisonPhrase> list, List<TNote> oldNotes, List<TNote> newNotes, bool includeChildNotesInNoteCount, uint tick) where TNote : Note<TNote>
        {
            var currentOldNoteIndex = 0;
            var currentNewNoteIndex = 0;
            foreach (var unisonPhrase in list)
            {
                if (unisonPhrase.TickEnd < tick)
                {
                    // Who cares, this unison phrase is in the past
                    continue;
                }
                var noteDelta = 0;
                while (currentOldNoteIndex < oldNotes.Count &&
                    oldNotes[currentOldNoteIndex].Tick < unisonPhrase.Tick)
                {
                    currentOldNoteIndex++;
                }
                while (currentNewNoteIndex < newNotes.Count &&
                    newNotes[currentNewNoteIndex].Tick < unisonPhrase.Tick)
                {
                    currentNewNoteIndex++;
                }

                while (currentOldNoteIndex < oldNotes.Count &&
                    oldNotes[currentOldNoteIndex].Tick < unisonPhrase.TickEnd && oldNotes[currentOldNoteIndex].Tick >= tick)
                {
                    // Subtract notes from the old list that are after the current tick and before the end of the unison phrase
                    noteDelta -= includeChildNotesInNoteCount ? oldNotes[currentOldNoteIndex].ChildNotes.Count + 1 : 1;
                    currentOldNoteIndex++;
                }
                while (currentNewNoteIndex < newNotes.Count &&
                    newNotes[currentNewNoteIndex].Tick < unisonPhrase.TickEnd && newNotes[currentNewNoteIndex].Tick >= tick)
                {
                    // Add notes from the new list that are after the current tick and before the end of the unison phrase
                    noteDelta += includeChildNotesInNoteCount ? newNotes[currentNewNoteIndex].ChildNotes.Count + 1 : 1;
                    currentNewNoteIndex++;
                }
                unisonPhrase.NoteCount += noteDelta;
            }
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