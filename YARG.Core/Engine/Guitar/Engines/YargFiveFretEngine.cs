using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(List<GuitarNote> notes, GuitarEngineParameters engineParameters) : base(notes,
            engineParameters)
        {
        }

        public override void UpdateBot(double songTime)
        {
            for (int i = State.NoteIndex; i < Notes.Count; i++)
            {
                var note = Notes[i];

                if (songTime >= note.Time)
                {
                    State.ButtonMask = (byte)note.NoteMask;
                    State.StrumLeniencyTimer = EngineParameters.StrumLeniency;
                    UpdateEngine(note.Time);
                    continue;
                }

                break;
            }
        }

        protected override bool UpdateHitLogic(double time)
        {
            double delta = time - LastUpdateTime;
            LastUpdateTime = time;

            if(State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            State.StrummedThisUpdate = IsInputUpdate && IsStrumInput(CurrentInput) && CurrentInput.Button;
            if (IsFretInput(CurrentInput))
            {
                ToggleFret(CurrentInput.Action, CurrentInput.Button);
            }

            UpdateTimers(delta);
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

                    if (CanNoteBeHit(next) && (State.StrummedThisUpdate || State.StrumLeniencyTimer > 0f || note.IsTap))
                    {
                        YargTrace.LogInfo("Skipping to hit next note as it is hittable");
                        HitNote(next);
                        return true;
                    }

                    next = next.NextNote;
                }

                return false;
            }

            if (State.StrummedThisUpdate || State.StrumLeniencyTimer > 0f || note.IsTap ||
                (note.IsHopo && EngineStats.Combo > 0))
            {
                HitNote(note);
                return true;
            }

            return false;
        }

        protected void UpdateTimers(double delta)
        {
            // If engine was invoked with an input update and the input is a fret
            // or: Did not strum at all
            // if (!State.StrummedThisUpdate)
            // {
            //     if (State.StrumLeniencyTimer > 0)
            //     {
            //         // Hopo leniency active and strum leniency active so hopo was strummed
            //         if (State.HopoLeniencyTimer > 0)
            //         {
            //             State.HopoLeniencyTimer = 0;
            //             State.StrumLeniencyTimer = 0;
            //         }
            //         else
            //         {
            //             State.StrumLeniencyTimer -= delta;
            //             if (State.StrumLeniencyTimer <= 0)
            //             {
            //                 YargTrace.DebugInfo("Strum leniency ended");
            //                 if (State.WasHopoStrummed)
            //                 {
            //                     State.StrumLeniencyTimer = 0;
            //                 }
            //                 else
            //                 {
            //                     Overstrum();
            //                 }
            //
            //                 State.WasHopoStrummed = false;
            //             }
            //         }
            //     }
            //     if (State.HopoLeniencyTimer > 0)
            //     {
            //         State.HopoLeniencyTimer -= delta;
            //     }
            // } else if (State.StrummedThisUpdate)
            // {
            //     if (State.HopoLeniencyTimer > 0)
            //     {
            //         State.StrumLeniencyTimer = 0;
            //     }
            //     else
            //     {
            //         // Strummed while strum leniency is already active, so double/triple strum
            //         if (State.StrumLeniencyTimer > 0)
            //         {
            //             Overstrum();
            //         }
            //
            //         State.StrumLeniencyTimer = EngineParameters.StrumLeniency;
            //         State.WasHopoStrummed = false;
            //     }
            // }
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

        protected override void HitNote(GuitarNote note)
        {
            if (note.IsHopo || note.IsTap)
            {
                // if (EngineStats.Combo > 0 && State.StrumLeniencyTimer > 0)
                // {
                //     EngineStats.Combo++;
                //     EngineStats.HoposStrummed++;
                //
                //     State.HopoLeniencyTimer = EngineParameters.HopoLeniency;
                //     State.StrumLeniencyTimer = EngineParameters.StrumLeniency / 2;
                //     State.WasHopoStrummed = true;
                // }
                // else
                // {
                //     State.StrumLeniencyTimer = 0;
                //     State.HopoLeniencyTimer = EngineParameters.HopoLeniency;
                // }

                State.StrumLeniencyTimer = 0;
            }
            else
            {
                State.StrumLeniencyTimer = 0;
            }

            base.HitNote(note);
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