using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums.Engines
{
    public class YargDrumsEngine : DrumsEngine
    {
        public YargDrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack, DrumsEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
        }

        protected override bool UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);

            DepleteStarPower(GetUsedStarPower());

            // Quits early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            var note = Notes[State.NoteIndex];

            return CheckForNoteHit();
        }

        protected override bool CheckForNoteHit()
        {
            var note = Notes[State.NoteIndex];

            // Note not in front end
            if (State.CurrentTime < note.Time + EngineParameters.FrontEnd)
            {
                return false;
            }

            if (State.CurrentTime > note.Time + EngineParameters.BackEnd)
            {
                MissNote(note);
                return true;
            }

            return false;
        }

        protected override bool CanNoteBeHit(DrumNote note) => throw new System.NotImplementedException();
    }
}