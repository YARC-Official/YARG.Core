using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    [Flags]
    public enum NoteFlags
    {
        None = 0,

        StarPower      = 1 << 0,
        StarPowerStart = 1 << 1,
        StarPowerEnd   = 1 << 2,

        SoloStart = 1 << 3,
        SoloEnd   = 1 << 4,
    }

    public abstract class Note<TNote> : ChartEvent
        where TNote : Note<TNote>
    {
        protected readonly List<TNote> _childNotes = new();
        private readonly NoteFlags  _flags;

        public NoteFlags Flags;

        private TNote _originalPreviousNote;
        private TNote _originalNextNote;

        public TNote PreviousNote;
        public TNote NextNote;

        public IReadOnlyList<TNote> ChildNotes => _childNotes;

        public bool IsChord => _childNotes.Count > 0;

        public bool IsStarPower      => (Flags & NoteFlags.StarPower) != 0;
        public bool IsStarPowerStart => (Flags & NoteFlags.StarPowerStart) != 0;
        public bool IsStarPowerEnd   => (Flags & NoteFlags.StarPowerEnd) != 0;

        public bool IsSoloStart => (Flags & NoteFlags.SoloStart) != 0;
        public bool IsSoloEnd   => (Flags & NoteFlags.SoloEnd) != 0;

        public bool WasHit;
        public bool WasMissed;

        protected Note(NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            _flags = flags;
            Flags = flags;
        }

        public virtual void AddChildNote(TNote note) {
            if (note.Tick != Tick || note.ChildNotes.Count > 0) {
                return;
            }

            _childNotes.Add(note);
        }

        public IEnumerable<TNote> ChordEnumerator()
        {
            yield return (TNote) this;
            foreach (var child in ChildNotes)
            {
                yield return child;
            }
        }

        public void SetHitState(bool hit, bool includeChildren)
        {
            WasHit = true;
            if (!includeChildren) return;

            foreach (var childNote in _childNotes)
            {
                childNote.SetHitState(hit, true);
            }
        }

        public void SetMissState(bool miss, bool includeChildren)
        {
            WasMissed = true;
            if (!includeChildren) return;

            foreach (var childNote in _childNotes)
            {
                childNote.SetMissState(miss, true);
            }
        }

        public void OverridePreviousNote()
        {
            // Prevent overriding previous note more than once without resetting note state
            if(_originalPreviousNote != null)
            {
                throw new InvalidOperationException("Cannot override previous note more than once");
            }

            _originalPreviousNote = PreviousNote;
            PreviousNote = null;
        }

        public void OverrideNextNote()
        {
            // Prevent overriding next note more than once without resetting note state
            if(_originalNextNote != null)
            {
                throw new InvalidOperationException("Cannot override next note more than once");
            }

            _originalNextNote = NextNote;
            NextNote = null;
        }

        public virtual void ResetNoteState()
        {
            Flags = _flags;
            WasHit = false;
            WasMissed = false;

            PreviousNote = _originalPreviousNote;
            NextNote = _originalNextNote;

            _originalPreviousNote = null;
            _originalNextNote = null;

            foreach(var childNote in _childNotes)
            {
                childNote.ResetNoteState();
            }
        }
    }
}