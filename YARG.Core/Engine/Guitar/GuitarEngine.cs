using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarAction, GuitarEngineParameters,
        GuitarStats, GuitarEngineState>
    {

        public delegate void OverstrumEvent();

        public OverstrumEvent OnOverstrum;

        protected GuitarEngine(List<GuitarNote> notes, GuitarEngineParameters engineParameters) : base(notes, engineParameters)
        {
        }

        protected virtual void Overstrum()
        {
            EngineStats.Combo = 0;
            EngineStats.Overstrums++;

            UpdateMultiplier();

            OnOverstrum?.Invoke();
        }

        protected override bool HitNote(GuitarNote note)
        {
            note.SetHitState(true, true);

            int notesSkipped = 0;
            var prevNote = note.PreviousNote;
            while (prevNote is not null && !prevNote.WasHit && !prevNote.WasMissed)
            {
                prevNote.SetMissState(true, true);
                prevNote = prevNote.PreviousNote;
                notesSkipped++;
                EngineStats.Combo = 0;
                EngineStats.NotesMissed++;
                State.NoteIndex++;
            }

            EngineStats.Combo++;
            EngineStats.NotesHit++;

            UpdateMultiplier();

            EngineStats.Score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count) * EngineStats.ScoreMultiplier;

            OnNoteHit?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
            return true;
        }

        protected override void MissNote(GuitarNote note)
        {
            note.SetMissState(true, true);

            EngineStats.Combo = 0;
            EngineStats.NotesMissed++;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
        }

        protected override void UpdateMultiplier()
        {
            EngineStats.ScoreMultiplier = EngineStats.Combo switch
            {
                >= 30 => 4,
                >= 20 => 3,
                >= 10 => 2,
                _     => 1
            };

            if (EngineStats.IsStarPowerActive)
            {
                EngineStats.ScoreMultiplier *= 2;
            }
        }

        protected void ToggleFret(int fret, bool active)
        {
            State.ButtonMask = (byte)(active ? State.ButtonMask | (1 << fret) : State.ButtonMask & ~(1 << fret));
        }

        public bool IsFretHeld(GuitarAction fret)
        {
            return (State.ButtonMask & (1 << (int)fret)) != 0;
        }
    }
}