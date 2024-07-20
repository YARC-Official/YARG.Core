//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace YARG.Core.Chart
{
    /// <summary>
    /// Tracks the current note of a note list across ticks.
    /// </summary>
    /// <remarks>
    /// Tracks child notes in addition to the parent notes contained directly within the given list.
    /// </remarks>
    public class NoteTickTracker<TNote>
        where TNote : Note<TNote>
    {
        private ChartEventTickTracker<TNote> _tracker;
        private int _childIndex = -1;

        public TNote? Current => _childIndex < 0 ? CurrentParent : CurrentChild;

        public TNote? CurrentParent => _tracker.Current;
        public TNote? CurrentChild => _childIndex >= 0 ? CurrentParent!.ChildNotes[_childIndex] : null;

        public int CurrentParentIndex => _tracker.CurrentIndex;
        public int CurrentChildIndex => _childIndex;

        public NoteTickTracker(List<TNote> notes)
        {
            _tracker = new(notes);
        }

        /// <summary>
        /// Updates the state of the note tracker to the given tick.
        /// </summary>
        /// <remarks>
        /// Resets the child note state upon a successful update.
        /// </remarks>
        /// <returns>
        /// True if a new note has been reached, false otherwise.
        /// </returns>
        public bool Update(uint tick)
        {
            if (_tracker.Update(tick))
            {
                _childIndex = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the state of the note tracker to the given tick by a single note.
        /// </summary>
        /// <remarks>
        /// First updates through a note's child notes, then updates to the next note.
        /// </remarks>
        /// <returns>
        /// True if a new note has been reached, false otherwise.
        /// </returns>
        public bool UpdateOnce(uint tick, [NotNullWhen(true)] out TNote? current)
        {
            var currentParent = CurrentParent;
            if (currentParent != null && _childIndex + 1 < currentParent.ChildNotes.Count)
            {
                _childIndex++;
                current = currentParent.ChildNotes[_childIndex];
                return true;
            }

            if (_tracker.UpdateOnce(tick, out current))
            {
                _childIndex = -1;
                return true;
            }

            current = Current;
            return false;
        }

        /// <summary>
        /// Resets the state of the note tracker.
        /// </summary>
        public void Reset()
        {
            _tracker.Reset();
            _childIndex = -1;
        }

        /// <summary>
        /// Resets the state of the note tracker to the given tick.
        /// </summary>
        public void ResetToTick(uint tick)
        {
            _tracker.ResetToTick(tick);
            _childIndex = -1;
        }
    }

    /// <summary>
    /// Tracks the current note of a note list across times.
    /// </summary>
    /// <remarks>
    /// Tracks child notes in addition to the parent notes contained directly within the given list.
    /// </remarks>
    public class NoteTimeTracker<TNote>
        where TNote : Note<TNote>
    {
        private ChartEventTimeTracker<TNote> _tracker;
        private int _childIndex = -1;

        public TNote? Current => _childIndex < 0 ? CurrentParent : CurrentChild;

        public TNote? CurrentParent => _tracker.Current;
        public TNote? CurrentChild => _childIndex >= 0 ? CurrentParent!.ChildNotes[_childIndex] : null;

        public int CurrentParentIndex => _tracker.CurrentIndex;
        public int CurrentChildIndex => _childIndex;

        public NoteTimeTracker(List<TNote> notes)
        {
            _tracker = new(notes);
        }

        /// <summary>
        /// Updates the state of the note tracker to the given time.
        /// </summary>
        /// <remarks>
        /// Resets the child note state upon a successful update.
        /// </remarks>
        /// <returns>
        /// True if a new note has been reached, false otherwise.
        /// </returns>
        public bool Update(double time)
        {
            if (_tracker.Update(time))
            {
                _childIndex = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the state of the note tracker to the given time by a single note.
        /// </summary>
        /// <remarks>
        /// First updates through a note's child notes, then updates to the next note.
        /// </remarks>
        /// <returns>
        /// True if a new note has been reached, false otherwise.
        /// </returns>
        public bool UpdateOnce(double time, [NotNullWhen(true)] out TNote? current)
        {
            var currentParent = CurrentParent;
            if (currentParent != null && _childIndex + 1 < currentParent.ChildNotes.Count)
            {
                _childIndex++;
                current = currentParent.ChildNotes[_childIndex];
                return true;
            }

            if (_tracker.UpdateOnce(time, out current))
            {
                _childIndex = -1;
                return true;
            }

            current = Current;
            return false;
        }

        /// <summary>
        /// Resets the state of the note tracker.
        /// </summary>
        public void Reset()
        {
            _tracker.Reset();
            _childIndex = -1;
        }

        /// <summary>
        /// Resets the state of the note tracker to the given time.
        /// </summary>
        public void ResetToTime(double time)
        {
            _tracker.ResetToTime(time);
            _childIndex = -1;
        }
    }

}