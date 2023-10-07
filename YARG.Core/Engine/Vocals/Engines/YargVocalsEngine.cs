using System;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals.Engines
{
    public class YargVocalsEngine : VocalsEngine
    {
        public YargVocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack, VocalsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }

        protected override bool UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);

            DepleteStarPower(GetUsedStarPower());

            // Get the pitch this update
            if (IsInputUpdate && CurrentInput.GetAction<VocalsAction>() == VocalsAction.Pitch)
            {
                State.InputTick = State.CurrentTick;
                State.PitchSangThisUpdate = CurrentInput.Axis;
            }

            // Quits early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            // If an input was detected, and this tick has not been processed, process it
            bool hitProcessed = false;
            if (IsInputUpdate &&
                State.PhraseTicksProcessed < State.CurrentTick)
            {
                bool noteHit = CheckForNoteHit();

                if (noteHit)
                {
                    // We have to do some math here in order to stay deterministic

                    var ticksProcessedSinceInput = (int) (State.PhraseTicksProcessed - State.InputTick);

                    if (ticksProcessedSinceInput < State.InputLeniencyTicks)
                    {
                        var tickDiff = State.CurrentTick - State.PhraseTicksProcessed;
                        var ticksSinceInput = State.CurrentTick - State.InputTick;

                        if (ticksSinceInput > State.InputLeniencyTicks)
                        {
                            var leniencyDiff = ticksSinceInput - State.InputLeniencyTicks;
                            tickDiff -= leniencyDiff;
                        }

                        State.PhraseTicksHit += tickDiff;

                        hitProcessed = true;
                    }
                }
            }

            OnSingTick?.Invoke(hitProcessed);

            // If there are any ticks that were missed between now and the last update, catch up.
            // This solves problems with this engines deterministic-ness, as updates don't happen if
            // there are no inputs or frames.
            State.PhraseTicksProcessed = State.CurrentTick;

            // Check for end of phrase
            var phrase = Notes[State.NoteIndex];
            if (phrase.TickEnd <= State.CurrentTick)
            {
                double percentHit = (double) State.PhraseTicksHit / State.PhraseTicksProcessed;

                // if (percentHit >= EngineParameters.PhraseHitPercent)
                // {
                //     HitNote(phrase);
                // }
                // else
                // {
                //     MissNote(phrase);
                // }

                // TODO: Proper scoring

                HitNote(phrase);
                EngineStats.Score += (int) State.PhraseTicksHit;
                UpdateStars();

                State.PhraseTicksHit = 0;
            }

            // Vocals never need a re-update
            return false;
        }

        protected override bool CheckForNoteHit()
        {
            // Stop early if nothing is being sang
            if (State.PitchSangThisUpdate == null) return false;

            var phrase = Notes[State.NoteIndex];

            // Not hittable if the phrase is after the current tick
            if (State.CurrentTick < phrase.Tick) return false;

            // Find the note within the phrase
            VocalNote? note = null;
            foreach (var phraseNote in phrase.ChildNotes)
            {
                // If in bounds, this is the note!
                if (State.CurrentTick > phraseNote.Tick &&
                    State.CurrentTick < phraseNote.TotalTickEnd)
                {
                    note = phraseNote;
                    break;
                }
            }

            // No note found to hit
            if (note == null) return false;

            OnTargetNoteChanged?.Invoke(note);

            return CanNoteBeHit(note);
        }

        protected override bool CanNoteBeHit(VocalNote note)
        {
            if (State.PitchSangThisUpdate == null) return false;

            // Octave does not matter
            float notePitch = note.PitchAtSongTick(State.CurrentTick) % 12f;
            float singPitch = State.PitchSangThisUpdate.Value % 12f;
            float dist = Math.Abs(singPitch - notePitch);

            // Try to check once within the range and...
            if (dist <= EngineParameters.HitWindow)
            {
                return true;
            }

            // ...try again twelve notes (one octave) away.
            // This effectively allows wrapping in the check. Only subtraction is needed
            // since we take the absolute value.
            if (dist - 12f <= EngineParameters.HitWindow)
            {
                return true;
            }

            return false;
        }
    }
}