using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums.Engines
{
    public class YargDrumsEngine : DrumsEngine
    {
        public YargDrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack, DrumsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            if (gameInput.Button)
            {
                State.LastPadHit = ConvertInputToPad(EngineParameters.Mode, gameInput.GetAction<DrumsAction>());
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateStarPower();

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[State.NoteIndex];
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            // Check for note miss note (back end)
            if (State.CurrentTime >= note.Time + EngineParameters.HitWindow.GetBackEnd(hitWindow))
            {
                foreach (var chordNote in note.ChordEnumerator())
                {
                    if (chordNote.WasHit || chordNote.WasMissed)
                    {
                        continue;
                    }

                    // Check for activation notes that weren't hit, and auto-hit them.
                    // This may seem weird, but it prevents issues from arising when scoring
                    // activation notes.
                    if (chordNote.IsStarPowerActivator && EngineStats.CanStarPowerActivate)
                    {
                        HitNote(chordNote, true);
                        continue;
                    }

                    MissNote(chordNote);
                }

                return;
            }

            // Check for note hit
            if (State.LastPadHit is not null)
            {
                var inputEaten = ProcessNoteHit(note);

                if (!inputEaten)
                {
                    // If the input was not consumed, then overhit
                    Overhit();

                    // At this point, either way, the input was consumed
                }

                OnPadHit?.Invoke((DrumsAction) State.LastPadHit.Value - 1, inputEaten);
                State.LastPadHit = null;

                if (inputEaten)
                {
                    return;
                }
            }

            return;
        }

        private bool ProcessNoteHit(DrumNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // Pass on the input
                return false;
            }

            // Remember that while playing drums, all notes of a chord don't have to be hit.
            // Treat all notes separately.
            foreach (var chordNote in note.ChordEnumerator())
            {
                if (chordNote.WasHit || chordNote.WasMissed)
                {
                    continue;
                }

                if (chordNote.Pad == State.LastPadHit)
                {
                    HitNote(chordNote);

                    // Eat the input
                    return true;
                }
            }

            // If that fails, attempt to hit any of the other notes ahead of this one (in the hit window)
            // This helps a lot with combo regain, especially with fast double bass.
            if (note.NextNote is not null && ProcessNoteHit(note.NextNote))
            {
                // Eat the input
                return true;
            }

            // Pass on the input
            return false;
        }

        public override void UpdateBot(double songTime)
        {
            throw new System.NotImplementedException();
        }

        protected override void CheckForNoteHit() => throw new System.NotImplementedException();

        protected override bool CanNoteBeHit(DrumNote note) => throw new System.NotImplementedException();
    }
}