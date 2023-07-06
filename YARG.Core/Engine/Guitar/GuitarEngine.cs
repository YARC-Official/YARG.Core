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

        protected override void HitNote(GuitarNote note)
        {
            note.SetHitState(true, true);

            int notesSkipped = 0;
            var prevNote = note.PreviousNote;
            while (prevNote is not null && !prevNote.WasHit && !prevNote.WasMissed)
            {
                prevNote.SetMissState(true, true);
                prevNote = prevNote.PreviousNote;
                notesSkipped++;
                EngineStats.NotesMissed++;
            }

            EngineStats.Combo++;
            EngineStats.NotesHit++;

            // Dont know what I'm doing with the note index just yet
            OnNoteHit?.Invoke(0, note);
        }

        protected override void MissNote(GuitarNote note)
        {
            note.SetMissState(true, true);

            EngineStats.Combo = 0;
            EngineStats.NotesMissed++;

            OnNoteMissed?.Invoke(0, note);
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
    }
}