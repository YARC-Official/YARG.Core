using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    public abstract class Note : ChartEvent
    {
        private readonly List<Note> _childNotes = new();
        private readonly NoteFlags  _flags;

        public Note previousNote;
        public Note nextNote;

        public IReadOnlyList<Note> ChildNotes => _childNotes;

        public bool IsChord => _childNotes.Count > 0;
		
        public bool IsStarPower      => (_flags & NoteFlags.StarPower) != 0;
        public bool IsStarPowerStart => (_flags & NoteFlags.StarPowerStart) != 0;
        public bool IsStarPowerEnd   => (_flags & NoteFlags.StarPowerEnd) != 0;
		
        public bool IsSoloStart => (_flags & NoteFlags.SoloStart) != 0;
        public bool IsSoloEnd   => (_flags & NoteFlags.SoloEnd) != 0;
        
        public bool WasHit    { get; private set; }
        public bool WasMissed { get; private set; }

        protected Note(NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            _flags = flags;
        }

        public virtual void AddChildNote(Note note) {
            if (note.Tick != Tick || note.ChildNotes.Count > 0) {
                return;
            }
			
            _childNotes.Add(note);
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
    }

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
}