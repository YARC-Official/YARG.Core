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
                State.PitchSangThisUpdate = CurrentInput.Axis;
            }
            else if (!IsBotUpdate)
            {
                State.PitchSangThisUpdate = null;
            }

            // TODO: Add some sort of leniency timer as microphone inputs don't happen every tick

            // Quits early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            // If an input was detected, and this tick has not been processed, process it
            if (State.PitchSangThisUpdate != null &&
                State.PhraseTicksProcessed < State.CurrentTick)
            {
                bool noteHit = CheckForNoteHit();

                if (noteHit)
                {
                    State.PhraseTicksHit++;
                }
            }

            // If there are any ticks that were missed between now and the last update, catch up.
            // This solves problems with this engines deterministic-ness, as updates don't happen if
            // there are no inputs or frames.
            State.PhraseTicksProcessed = State.CurrentTick;

            // Check for end of phrase
            var phrase = Notes[State.NoteIndex];
            if (phrase.TickEnd >= State.CurrentTick)
            {
                double percentHit = (double) State.PhraseTicksHit / State.PhraseTicksProcessed;

                if (percentHit >= EngineParameters.PhraseHitPercent)
                {
                    HitNote(phrase);
                }
                else
                {
                    MissNote(phrase);
                }
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
            if (phrase.Tick > State.CurrentTick) return false;

            // Find the note within the phrase
            VocalNote? note = null;
            foreach (var phraseNote in phrase.ChildNotes)
            {
                // There are no more hittable notes at this point (phrase is sorted)
                if (State.CurrentTick < phraseNote.Tick) break;

                // If in bounds, this is the note!
                if (State.CurrentTick > phraseNote.Tick &&
                    State.CurrentTick < phraseNote.TotalTickLength)
                {
                    note = phraseNote;
                    break;
                }
            }

            // No note found to hit
            if (note == null) return false;

            return CanNoteBeHit(note);
        }

        protected override bool CanNoteBeHit(VocalNote note)
        {
            if (State.PitchSangThisUpdate == null) return false;

            float pitch = note.PitchAtSongTick(State.CurrentTick);
            return Math.Abs(State.PitchSangThisUpdate.Value - pitch) <= EngineParameters.HitWindow;
        }
    }
}