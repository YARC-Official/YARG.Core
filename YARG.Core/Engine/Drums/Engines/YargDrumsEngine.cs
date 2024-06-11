using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Drums.Engines
{
    public class YargDrumsEngine : DrumsEngine
    {
        public YargDrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack,
            DrumsEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            if (gameInput.Button)
            {
                State.PadHit = ConvertInputToPad(EngineParameters.Mode, gameInput.GetAction<DrumsAction>());
                State.HitVelocity = gameInput.Axis;
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();

            // Update bot (will return if not enabled)
            UpdateBot(time);

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                ResetPadState();
                return;
            }

            CheckForNoteHit();
        }

        protected override void CheckForNoteHit()
        {
            for (int i = State.NoteIndex; i < Notes.Count; i++)
            {
                bool isFirstNoteInWindow = i == State.NoteIndex;
                bool stopSkipping = false;

                var parentNote = Notes[i];

                // For drums, each note in the chord are treated separately
                foreach (var note in parentNote.AllNotes)
                {
                    // Miss out the back end
                    if (!IsNoteInWindow(note, out bool missed))
                    {
                        if (isFirstNoteInWindow && missed)
                        {
                            // If one of the notes in the chord was missed out the back end,
                            // that means all of them would miss.
                            foreach (var missedNote in parentNote.AllNotes)
                            {
                                MissNote(missedNote);
                            }
                        }

                        // You can't skip ahead if the note is not in the hit window to begin with
                        stopSkipping = true;
                        break;
                    }

                    // Hit note
                    if (CanNoteBeHit(note))
                    {
                        bool awardVelocityBonus = ApplyVelocity(note);

                        // TODO - Deadly Dynamics modifier check on awardVelocityBonus
                        HitNote(note, awardVelocityBonus, false);
                        ResetPadState();

                        // You can't hit more than one note with the same input
                        stopSkipping = true;
                        break;
                    }
                    else
                    {
                        YargLogger.LogFormatDebug("Cant hit note (Index: {0}) at {1}.", i, State.CurrentTime);
                    }
                }

                if (stopSkipping)
                {
                    break;
                }
            }

            // If no note was hit but the user hit a pad, then over hit
            if (State.PadHit != null)
            {
                Overhit();
                ResetPadState();
            }
        }

        protected void HitNote(DrumNote note, bool awardVelocityBonus, bool activationAutoHit)
        {
            HitNote(note, activationAutoHit);

            if (awardVelocityBonus){
                int velocityBonus = (int)(POINTS_PER_NOTE * 0.5 * EngineStats.ScoreMultiplier);
                AddScore(velocityBonus);
                YargLogger.LogFormatTrace("Velocity bonus of {0} points was awarded to a note at tick {1}.", velocityBonus, note.Tick);
            }
        }

        protected override bool CanNoteBeHit(DrumNote note)
        {
            return note.Pad == State.PadHit;
        }

        protected override void UpdateBot(double time)
        {
            if (!IsBot || State.NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[State.NoteIndex];

            if (time < note.Time)
            {
                return;
            }

            // Each note in the "chord" is hit separately on drums
            foreach (var chordNote in note.AllNotes)
            {
                State.PadHit = chordNote.Pad;
                CheckForNoteHit();
            }
        }

        private void ResetPadState()
        {
            State.PadHit = null;
            State.HitVelocity = null;
        }
    }
}