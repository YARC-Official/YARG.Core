using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys.Engines
{
    public class YargProKeysEngine : ProKeysEngine
    {
        public YargProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot) : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<ProKeysAction>();

            if (action is ProKeysAction.StarPower)
            {
                // TODO
            }
            else if (action is ProKeysAction.TouchEffects)
            {
                // TODO
            }
            else if (gameInput.Button)
            {
                State.KeyHit = (int) action;
                State.KeyMask |= (int) action;
            }
            else if (!gameInput.Button)
            {
                State.KeyReleased = (int) action;
                State.KeyMask &= ~(int) action;
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();

            // Update bot (will return if not enabled)
            // UpdateBot(time);

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                State.KeyHit = null;
                State.KeyReleased = null;
                return;
            }

            CheckForNoteHit();
        }

        protected override void CheckForNoteHit()
        {
            var parentNote = Notes[State.NoteIndex];

            // For pro-keys, each note in the chord are treated separately
            foreach (var note in parentNote.ChordEnumerator())
            {
                // Miss out the back end
                if (!IsNoteInWindow(note, out bool missed))
                {
                    if (missed)
                    {
                        // If one of the notes in the chord was missed out the back end,
                        // that means all of them would miss.
                        foreach (var missedNote in parentNote.ChordEnumerator())
                        {
                            MissNote(missedNote);
                        }
                    }

                    break;
                }

                // Hit note
                if (CanNoteBeHit(note))
                {
                    HitNote(note);
                    State.KeyHit = null;

                    break;
                }
            }

            // If no note was hit but the user hit a key, then over hit
            if (State.KeyHit != null)
            {
                Overhit();
                State.KeyHit = null;
            }
        }

        protected override bool CanNoteBeHit(ProKeysNote note)
        {
            return note.Key == State.KeyHit;
        }

        protected override void UpdateBot(double time)
        {
            // throw new System.NotImplementedException();
        }
    }
}