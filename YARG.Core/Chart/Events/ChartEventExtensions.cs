using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static List<TNote> DuplicateNotes<TNote>(this List<TNote> notes)
            where TNote : Note<TNote>
        {
            int count = notes.Count;
            var newNotes = new List<TNote>(count);
            if (count < 1)
                return newNotes;

            // Clone first note separately so we can link everything together
            var previousNote = notes[0].Clone();
            newNotes.Add(previousNote);

            // Clone the rest
            for (int i = 1; i < notes.Count; i++)
            {
                var newNote = notes[i].Clone();

                // Assign forward/backward references
                newNote.PreviousNote = previousNote;
                foreach (var child in newNote.ChildNotes)
                    child.PreviousNote = previousNote;

                previousNote.NextNote = newNote;
                foreach (var child in previousNote.ChildNotes)
                    child.NextNote = newNote;

                // Add note to list
                newNotes.Add(newNote);
                previousNote = newNote;
            }

            return newNotes;
        }

        // Remove note from list in place, adjusting references as required
        public static void RemoveNoteAt(this List<GuitarNote> notes, int index)
        {
            if (notes[index].ChildNotes.Count == 0)
            {
                // The simple case, when there are no children
                // Well, simple except for having to deal with all the flags that may need to move
                if (notes[index].IsSoloStart && !notes[index].IsSoloEnd)
                {
                    // This is a single note that is a solo start, we have to move it to the
                    // NEXT note (we don't want to extend the solo).
                    if (index < notes.Count)
                    {
                        notes[index + 1].Flags |= NoteFlags.SoloStart;
                        // Also add it to the child notes
                        foreach (var childNote in notes[index].ChildNotes)
                        {
                            childNote.Flags |= NoteFlags.SoloStart;
                        }
                    }
                }

                if (notes[index].IsSoloEnd)
                {
                    // This is a single kick drum note that is a solo end, we have to move it to the
                    // PREVIOUS note (we don't want to extend the solo).
                    if (index > 0)
                    {
                        notes[index - 1].Flags |= NoteFlags.SoloEnd;
                        // Also add it to the child notes
                        foreach (var childNote in notes[index - 1].ChildNotes)
                        {
                            childNote.Flags |= NoteFlags.SoloEnd;
                        }
                    }
                }


                if (notes[index].IsStarPowerStart && !notes[index].IsStarPowerEnd)
                {
                    // Make the next note the SP start, so as to not extend the SP section
                    if (index < notes.Count)
                    {
                        notes[index + 1].Flags |= NoteFlags.StarPowerStart;
                        // Also add it to the child notes
                        foreach (var childNote in notes[index].ChildNotes)
                        {
                            childNote.Flags |= NoteFlags.StarPowerStart;
                        }
                    }
                }

                if (notes[index].IsStarPowerEnd)
                {
                    // This is a single kick drum note that is a starpower end, we have to move it to the
                    // PREVIOUS note (we don't want to extend the starpower section).
                    if (index > 0)
                    {
                        notes[index - 1].Flags |= NoteFlags.StarPowerEnd;
                        // Also add it to the child notes
                        foreach (var childNote in notes[index - 1].ChildNotes)
                        {
                            childNote.Flags |= NoteFlags.StarPowerEnd;
                        }
                    }
                }

                notes.RemoveAt(index);

                // Fix Previous/Next references
                if (index == 0)
                {
                    // We were the first, so new 0's previous becomes null and everything else is fine
                    notes[0].PreviousNote = null;
                    return;
                }

                if (index == notes.Count)
                {
                    // We were the last, so new last's next becomes null and everything else is fine
                    notes[^1].NextNote = null;
                    return;
                }

                // We're somewhere in the middle, so we have to stitch references back together
                notes[index - 1].NextNote = notes[index];
                notes[index].PreviousNote = notes[index - 1];
            }
            else
            {
                // Promote first child to parent, reparent other children, and stitch references
                var newParent = notes[index].ChildNotes[0].Clone();
                for (var i = 1; i < notes[index].ChildNotes.Count; i++)
                {
                    newParent.AddChildNote(notes[index].ChildNotes[i]);
                }

                if (index > 0)
                {
                    newParent.PreviousNote = notes[index - 1];
                    notes[index - 1].NextNote = newParent;
                }
                else
                {
                    newParent.PreviousNote = null;
                }

                if (index < notes.Count - 1)
                {
                    newParent.NextNote = notes[index + 1];
                    notes[index + 1].PreviousNote = newParent;
                }
                else
                {
                    newParent.NextNote = null;
                }

                notes[index] = newParent;
            }
        }

        /// <summary>
        /// Creates a new note group not including the indicated child
        /// </summary>
        /// <param name="notes"></param>
        /// <param name="index"></param>
        /// <param name="childIndex"></param>
        public static void RemoveChildFromNote(this List<GuitarNote> notes, int index, int childIndex)
        {
            var newNote = notes[index].CloneWithoutChildNotes();

            for(int i = 0; i < notes[index].ChildNotes.Count; i++)
            {
                if (childIndex == i)
                {
                    continue;
                }

                newNote.AddChildNote(notes[index].ChildNotes[i]);
            }

            // Stitch references
            if (index > 0)
            {
                newNote.PreviousNote = notes[index - 1];
                notes[index - 1].NextNote = newNote;
            }
            else
            {
                newNote.PreviousNote = null;
            }

            if (index < notes.Count - 1)
            {
                newNote.NextNote = notes[index + 1];
                notes[index + 1].PreviousNote = newNote;
            }
            else
            {
                newNote.NextNote = null;
            }

            notes[index] = newNote;
        }

        public static double GetStartTime<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Time;
        }

        public static double GetEndTime<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TimeEnd;
        }

        public static uint GetFirstTick<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TickEnd;
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBound"/>
        public static int LowerBound<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.LowerBound(time, EventComparer<TEvent>.CompareTime, before: true);
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBound"/>
        public static int LowerBound<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.LowerBound(tick, EventComparer<TEvent>.CompareTick, before: true);
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBoundElement"/>
        public static bool LowerBoundElement<TEvent>(
            this List<TEvent> events, double time, [MaybeNullWhen(false)] out TEvent value
        )
            where TEvent : ChartEvent
        {
            return events.LowerBoundElement(time, EventComparer<TEvent>.CompareTime, before: true, out value);
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBoundElement"/>
        public static bool LowerBoundElement<TEvent>(
            this List<TEvent> events, uint tick, [MaybeNullWhen(false)] out TEvent value
        )
            where TEvent : ChartEvent
        {
            return events.LowerBoundElement(tick, EventComparer<TEvent>.CompareTick, before: true, out value);
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBoundElement"/>
        public static TEvent? LowerBoundElement<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.LowerBoundElement(time, EventComparer<TEvent>.CompareTime, before: true);
        }

        /// <summary>
        /// Searches for the first event that occurs before (or at) the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.LowerBoundElement"/>
        public static TEvent? LowerBoundElement<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.LowerBoundElement(tick, EventComparer<TEvent>.CompareTick, before: true);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBound"/>
        public static int UpperBound<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.UpperBound(time, EventComparer<TEvent>.CompareTime);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBound"/>
        public static int UpperBound<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.UpperBound(tick, EventComparer<TEvent>.CompareTick);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBoundElement"/>
        public static bool UpperBoundElement<TEvent>(
            this List<TEvent> events, double time, [MaybeNullWhen(false)] out TEvent value
        )
            where TEvent : ChartEvent
        {
            return events.UpperBoundElement(time, EventComparer<TEvent>.CompareTime, out value);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBoundElement"/>
        public static bool UpperBoundElement<TEvent>(
            this List<TEvent> events, uint tick, [MaybeNullWhen(false)] out TEvent value
        )
            where TEvent : ChartEvent
        {
            return events.UpperBoundElement(tick, EventComparer<TEvent>.CompareTick, out value);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given time.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBoundElement"/>
        public static TEvent? UpperBoundElement<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.UpperBoundElement(time, EventComparer<TEvent>.CompareTime);
        }

        /// <summary>
        /// Searches for the first event that occurs after the given tick.
        /// </summary>
        /// <seealso cref="Extensions.CollectionExtensions.UpperBoundElement"/>
        public static TEvent? UpperBoundElement<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.UpperBoundElement(tick, EventComparer<TEvent>.CompareTick);
        }

        /// <summary>
        /// Searches for all events that occur at the given time.
        /// </summary>
        public static bool FindEqualRange<TEvent>(this List<TEvent> events, double time, out Range range)
            where TEvent : ChartEvent
        {
            return events.FindEqualRange(time, EventComparer<TEvent>.CompareTime, out range);
        }

        /// <summary>
        /// Searches for all events that occur at the given tick.
        /// </summary>
        public static bool FindEqualRange<TEvent>(this List<TEvent> events, uint tick, out Range range)
            where TEvent : ChartEvent
        {
            return events.FindEqualRange(tick, EventComparer<TEvent>.CompareTick, out range);
        }

        /// <summary>
        /// Searches for all events that occur between the given start and end time.
        /// </summary>
        /// <param name="endInclusive">
        /// Whether the end value should be treated as inclusive, rather than exclusive.
        /// </param>
        public static bool FindRange<TEvent>(
            this List<TEvent> events, double startTime, double endTime, bool endInclusive, out Range range
        )
            where TEvent : ChartEvent
        {
            return events.FindRange(startTime, endTime, EventComparer<TEvent>.CompareTime, endInclusive, out range);
        }

        /// <summary>
        /// Searches for all events that occur between the given start and end tick.
        /// </summary>
        /// <param name="endInclusive">
        /// Whether the end value should be treated as inclusive, rather than exclusive.
        /// </param>
        public static bool FindRange<TEvent>(
            this List<TEvent> events, uint startTick, uint endTick, bool endInclusive, out Range range
        )
            where TEvent : ChartEvent
        {
            return events.FindRange(startTick, endTick, EventComparer<TEvent>.CompareTick, endInclusive, out range);
        }

        private static class EventComparer<TEvent>
            where TEvent : ChartEvent
        {
            public static readonly SearchComparison<TEvent, double> CompareTime = (ev, time) => ev.Time.CompareTo(time);
            public static readonly SearchComparison<TEvent, uint> CompareTick = (ev, tick) => ev.Tick.CompareTo(tick);
        }
    }
}
