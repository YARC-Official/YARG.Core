using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<GuitarAction>();

            if (action == GuitarAction.StarPower && gameInput.Button && EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
                return;
            }

            if (action is GuitarAction.StrumDown or GuitarAction.StrumUp && gameInput.Button)
            {
                State.DidStrum = true;
                return;
            }

            if (IsFretInput(gameInput))
            {
                ToggleFret(gameInput.Action, gameInput.Button);
                return;
            }
        }

        protected override bool UpdateEngineLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateStarPower();

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            var note = Notes[State.NoteIndex];
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            // Check for note miss note (back end)
            if (State.CurrentTime > note.Time + EngineParameters.HitWindow.GetBackEnd(hitWindow))
            {
                if (!note.WasFullyHitOrMissed())
                {
                    MissNote(note);
                    return true;
                }
            }

            // Check for note hit
            if (State.DidStrum)
            {
                var inputEaten = ProcessNoteHit(note);

                if (!inputEaten)
                {
                    // If the input was not eaten, then overstrum
                    Overstrum();

                    State.DidStrum = false;
                }
                else
                {
                    // If an input was eaten, a note was hit
                    State.DidStrum = false;
                    return true;
                }
            }

            return false;
        }

        private bool ProcessNoteHit(GuitarNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // Pass on the input
                return false;
            }

            if (State.ButtonMask == note.NoteMask)
            {
                HitNote(note);
                return true;
            }

            // Pass on the input
            return false;
        }

        public override void UpdateBot(double songTime)
        {
            throw new System.NotImplementedException();
        }

        protected override bool CheckForNoteHit() => throw new System.NotImplementedException();

        protected override bool CanNoteBeHit(GuitarNote note) => throw new System.NotImplementedException();
    }
}