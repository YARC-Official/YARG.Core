using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
        }

        public override void UpdateBot(double songTime)
        {
            base.UpdateBot(songTime);

            if (State.NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[State.NoteIndex];

            bool updateToSongTime = true;
            while (note is not null && songTime >= note.Time)
            {
                updateToSongTime = false;

                State.ButtonMask = (byte) note.NoteMask;
                State.StrummedThisUpdate = true;
                State.FrontEndTimer = note.Time;
                if (!UpdateHitLogic(note.Time))
                {
                    break;
                }

                note = note.NextNote;
            }

            State.StrummedThisUpdate = false;

            if (updateToSongTime)
            {
                UpdateHitLogic(songTime);
            }
        }

        protected override bool UpdateHitLogic(double time)
        {
            double delta = time - LastUpdateTime;
            LastUpdateTime = CurrentTime;

            CurrentTime = time;

            UpdateTimers(delta);

            State.StrummedThisUpdate = (IsInputUpdate && IsStrumInput(CurrentInput) && CurrentInput.Button)
                || (State.StrummedThisUpdate && IsBotUpdate);
            if (IsInputUpdate && IsFretInput(CurrentInput))
            {
                ToggleFret(CurrentInput.Action, CurrentInput.Button);
                State.FrontEndTimer = time;
            }

            if (State.ButtonMask != State.TapButtonMask)
            {
                State.TapButtonMask = 0;
            }

            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            // Update strum leniency if strummed this update
            if (State.StrummedThisUpdate)
            {
                if (State.StrumLeniencyTimer > 0)
                {
                    Overstrum();
                }

                State.StrumLeniencyTimer = EngineParameters.StrumLeniency;
            }

            var note = Notes[State.NoteIndex];

            if (note.WasHit || note.WasMissed)
            {
                return false;
            }

            if (time < note.Time + EngineParameters.FrontEnd)
            {
                return false;
            }

            if (time > note.Time + EngineParameters.BackEnd && !note.WasHit)
            {
                MissNote(note);
                return true;
            }

            // Note skipping, useful for combo regain
            if (!CanNoteBeHit(note))
            {
                if (EngineStats.Combo != 0)
                {
                    return false;
                }

                var next = note.NextNote;
                while (next is not null)
                {
                    if (time < next.Time + EngineParameters.FrontEnd)
                    {
                        return false;
                    }

                    // Don't need to check back end because if we're here then the previous note was not out of time

                    if (CanNoteBeHit(next) &&
                        (State.StrummedThisUpdate || State.StrumLeniencyTimer > 0f || note.IsTap) &&
                        State.TapButtonMask == 0)
                    {
                        if (HitNote(next))
                        {
                            YargTrace.LogInfo($"Skipping to hit next note as it is hittable ({State.TapButtonMask})");
                            return true;
                        }
                    }

                    next = next.NextNote;
                }

                return false;
            }

            // Handles hitting a hopo/tap notes
            if (State.TapButtonMask == 0 && note.IsTap || (note.IsHopo && EngineStats.Combo > 0))
            {
                return HitNote(note);
            }

            // If hopo/tap checks failed then the note can be hit if it was strummed
            if (State.StrummedThisUpdate || State.StrumLeniencyTimer > 0f)
            {
                return HitNote(note);
            }

            return false;
        }

        protected void UpdateTimers(double delta)
        {
            // Timer will never decrease if infinite front end is enabled
            if (!EngineParameters.InfiniteFrontEnd && State.FrontEndTimer > 0)
            {
                State.FrontEndTimer -= delta;
            }

            if (State.StrumLeniencyTimer > 0)
            {
                // Add hopo leniency later

                State.StrumLeniencyTimer -= delta;
                if (State.StrumLeniencyTimer <= 0)
                {
                    Overstrum();
                }
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            // If open, must not hold any frets
            // If not open, must be holding at least 1 fret
            if (note.NoteMask == 0 && State.ButtonMask != 0 || note.NoteMask != 0 && State.ButtonMask == 0)
            {
                return false;
            }

            // If holding exact note mask, can hit
            if (State.ButtonMask == note.NoteMask)
            {
                return true;
            }

            // Anchoring

            // XORing the two masks will give the anchor around the note.
            int anchorButtons = State.ButtonMask ^ note.NoteMask;

            // Strum chord (cannot anchor)
            if (note.IsChord && note.IsStrum)
            {
                // Buttons must match note mask exactly for strum chords
                return State.ButtonMask == note.NoteMask;
            }

            // Anchoring single notes or hopo/tap chords

            // Anchor buttons held are lower than the note mask
            return anchorButtons < note.NoteMask;
        }

        protected override bool HitNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;

            if (note.IsHopo || note.IsTap)
            {
                // Disallow hitting if front end timer is not in range of note time
                if (State.FrontEndTimer <= 0)
                {
                    return false;
                }

                State.StrumLeniencyTimer = 0;
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.TapButtonMask = 0;

                // Does the same thing but ensures it still works when infinite front end is disabled
                State.FrontEndTimer = double.MaxValue;

                State.StrumLeniencyTimer = 0;
            }

            return base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;
            base.MissNote(note);
        }

        protected bool IsFretInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.Green or
                    GuitarAction.Red or
                    GuitarAction.Yellow or
                    GuitarAction.Blue or
                    GuitarAction.Orange => true,
                _ => false,
            };
        }

        protected bool IsStrumInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.StrumUp or
                    GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}