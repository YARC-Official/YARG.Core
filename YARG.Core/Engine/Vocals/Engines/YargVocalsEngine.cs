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

        public override void UpdateBot(double songTime)
        {
            base.UpdateBot(songTime);

            // Skip if there are no notes left
            if (State.NoteIndex >= Notes.Count) return;

            // Skip if the song hasn't started yet
            if (songTime < 0) return;

            // Skip if the song time and the current time is basically the same
            if (Math.Abs(songTime - State.CurrentTime) <= double.Epsilon) return;

            // This is a little more tricky since vocals requires a constant stream of inputs.
            // We can use the ApproximateVocalFps from 0s songTime to determine the amount of inputs
            // (or rather updates) we need to apply.
            double spf = 1.0 / EngineParameters.ApproximateVocalFps;

            // First, get the first update after the last time
            // (CurrentTime would the be last time at this point)
            double first;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            // Since CurrentTime is set equal to `double.MinValue`, this should work
            if (State.CurrentTime == double.MinValue)
            {
                // If the last update time is not set, just assume 0
                first = 0;
            }
            else
            {
                first = Math.Ceiling(State.CurrentTime / spf) * spf;
            }

            // Push out a bunch of updates
            for (double time = first; time < songTime; time += spf)
            {
                // Skip all updates that are in the past
                if (time <= State.CurrentTime) continue;

                var phrase = Notes[State.NoteIndex];
                var note = GetNoteInPhraseAtSongTick(phrase, State.CurrentTick);

                if (note is not null && !note.IsPercussion)
                {
                    State.DidSing = true;
                    State.PitchSang = note.PitchAtSongTime(State.CurrentTime);
                    State.LastSingTime = time;
                }
                else
                {
                    State.DidSing = false;
                }

                UpdateHitLogic(time);
            }
        }

        protected override bool UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);

            // Activate starpower
            if (IsInputUpdate && CurrentInput.GetAction<VocalsAction>() == VocalsAction.StarPower &&
                EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
            }

            DepleteStarPower(GetUsedStarPower());

            // Get the pitch this update
            if (IsInputUpdate && CurrentInput.GetAction<VocalsAction>() == VocalsAction.Pitch)
            {
                State.DidSing = true;
                State.PitchSang = CurrentInput.Axis;

                State.LastSingTime = State.CurrentTime;
            }
            else if (!IsBotUpdate)
            {
                State.DidSing = false;
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
            if (State.DidSing)
            {
                bool noteHit = CheckForNoteHit();

                if (noteHit)
                {
                    State.PhraseTicksHit++;
                    State.LastHitTime = State.CurrentTime;
                }
                else
                {
                    // If star power can activate, there is no note to hit, and the player
                    // hasn't sang in 0.25 seconds, then activate starpower.
                    if (EngineParameters.SingToActivateStarpower &&
                        GetNoteInPhraseAtSongTick(phrase, State.CurrentTick) is null &&
                        EngineStats.CanStarPowerActivate &&
                        State.CurrentTime - State.LastHitTime > 0.5)
                    {
                        ActivateStarPower();
                    }
                }
            }

            // Check for end of phrase
            if (State.CurrentTick > phrase.TickEnd)
            {
                // Get the percent hit. If there's no notes, 100% was hit.
                double percentHit = State.PhraseTicksHit / State.PhraseTicksTotal.Value;
                if (State.PhraseTicksTotal.Value == 0)
                {
                    percentHit = 1.0;
                }

                bool hit = percentHit >= EngineParameters.PhraseHitPercent;
                if (hit)
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

                OnPhraseHit?.Invoke(percentHit / EngineParameters.PhraseHitPercent, hit);
            }

            // Vocals never need a re-update
            return false;
        }

        protected override bool CanNoteBeHit(VocalNote note)
        {
            // Non-pitched notes (talkies) can be hit always
            if (note.IsNonPitched)
            {
                return true;
            }

            // Octave does not matter
            float notePitch = note.PitchAtSongTime(State.CurrentTime) % 12f;
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