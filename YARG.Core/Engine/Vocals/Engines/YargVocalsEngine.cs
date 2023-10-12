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
                State.PitchSang = CurrentInput.Axis;
                State.VisualLastSingTime = State.CurrentTime;
            }

            // Quits early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            // Set phrase ticks if not set
            var phrase = Notes[State.NoteIndex];
            State.PhraseTicksTotal ??= GetVocalTicksInPhrase(phrase);

            // If an input was detected, and this tick has not been processed, process it
            if (IsInputUpdate || IsBotUpdate)
            {
                bool noteHit = CheckForNoteHit();

                if (noteHit)
                {
                    State.PhraseTicksHit++;
                    State.VisualLastHitTime = State.CurrentTime;
                }
            }

            // Check for end of phrase
            if (State.CurrentTick > phrase.TickEnd)
            {
                double percentHit = State.PhraseTicksHit / State.PhraseTicksTotal.Value;

                if (percentHit >= EngineParameters.PhraseHitPercent)
                {
                    // Update stats (always add 100% of the phrase when hit)
                    EngineStats.VocalTicksHit += (uint) State.PhraseTicksTotal.Value;

                    HitNote(phrase);
                }
                else
                {
                    // Update stats (just do it normally here)
                    EngineStats.VocalTicksHit += State.PhraseTicksHit;
                    EngineStats.VocalTicksMissed += (uint) (State.PhraseTicksTotal.Value - State.PhraseTicksHit);

                    MissNote(phrase);
                }

                UpdateStars();

                // Update tick variables
                State.PhraseTicksHit = 0;
                State.PhraseTicksTotal = State.NoteIndex < Notes.Count ?
                    GetVocalTicksInPhrase(Notes[State.NoteIndex]) :
                    null;

                OnPhraseHit?.Invoke(percentHit / EngineParameters.PhraseHitPercent);
            }

            // Vocals never need a re-update
            return false;
        }

        protected override bool CanNoteBeHit(VocalNote note)
        {
            // Octave does not matter
            float notePitch = note.PitchAtSongTick(State.CurrentTick) % 12f;
            float singPitch = State.PitchSang % 12f;
            float dist = Math.Abs(singPitch - notePitch);

            // Try to check once within the range and...
            if (dist <= EngineParameters.HitWindow)
            {
                return true;
            }

            // ...try again twelve notes (one octave) away.
            // This effectively allows wrapping in the check. Only subtraction is needed
            // since we take the absolute value before hand and now.
            if (Math.Abs(dist - 12f) <= EngineParameters.HitWindow)
            {
                return true;
            }

            return false;
        }
    }
}