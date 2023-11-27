﻿using System;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class NewYargFiveFretEngine : GuitarEngine
    {
        public NewYargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack, GuitarEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
        }

        protected override bool UpdateHitLogic(double time)
        {
            UpdateTimeVariables(time);
            //UpdateTimers();
            
            DepleteStarPower(GetUsedStarPower());
            
            UpdateInput();
            
            if (State.IsStarPowerInputActive && EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
            }

            if (State.ButtonMask != State.TapButtonMask)
            {
                State.TapButtonMask = 0;
            }

            if (State.NoteIndex >= Notes.Count)
            {
                UpdateSustains();
                return false;
            }

            var note = Notes[State.NoteIndex];

            if (IsInputUpdate && IsFretInput(CurrentInput))
            {
                // Check for fret ghosting
                // We want to run ghost logic regardless of the setting (for the ghost counter)
                if (note.PreviousNote is not null)
                {
                    bool ghostedThisInput = CheckForGhostInput(note);

                    // This variable controls hit logic for ghosting
                    State.WasNoteGhosted = EngineParameters.AntiGhosting && (ghostedThisInput || State.WasNoteGhosted);

                    // Add ghost inputs to stats regardless of the setting for anti ghosting
                    if (ghostedThisInput)
                    {
                        EngineStats.GhostInputs++;
                    }
                }
            }

            bool isNoteHit = CheckForNoteHit();
            
            UpdateSustains();
            return isNoteHit;
        }

        protected override bool CheckForNoteHit()
        {
            var note = Notes[State.NoteIndex];

            if (note.WasHit || note.WasMissed)
            {
                return false;
            }
            
            if (State.CurrentTime < note.Time + EngineParameters.FrontEnd)
            {
                return false;
            }

            if (State.CurrentTime > note.Time + EngineParameters.BackEnd && !note.WasHit)
            {
                MissNote(note);
                return true;
            }

            if (!CanNoteBeHit(note))
            {
                return false;
            }
            
            // Handles hitting hopo/tap notes
            // If first note is a hopo then it can be hit without combo (for practice mode)
            bool canHitTap = State.TapButtonMask == 0;// && note.IsTap;
            bool canHitHopo = note.IsHopo && (EngineStats.Combo > 0 || State.NoteIndex == 0);
            
            if ((canHitTap || (canHitHopo && false)) && !State.WasNoteGhosted)
            {
                return HitNote(note);
            }

            return false;
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            byte buttonsMasked = State.ButtonMask;
            foreach (var sustainNote in ActiveSustains)
            {
                // Don't want to mask off the note we're checking otherwise it'll always return false lol
                if (note == sustainNote)
                {
                    continue;
                }

                // Mask off the disjoint mask if its disjointed or extended disjointed
                // This removes just the single fret of the disjoint note
                if ((sustainNote.IsExtendedSustain && sustainNote.IsDisjoint) || sustainNote.IsDisjoint)
                {
                    buttonsMasked -= (byte) sustainNote.DisjointMask;
                }
                else if (sustainNote.IsExtendedSustain)
                {
                    // Remove the entire note mask if its an extended sustain
                    // Difference between NoteMask and DisjointMask is that DisjointMask is only a single fret
                    // while NoteMask is the entire chord
                    buttonsMasked -= (byte) sustainNote.NoteMask;
                }
            }

            // Use the DisjointMask for comparison if disjointed and was hit (for sustain logic)
            int noteMask = (note.IsDisjoint || note.IsExtendedSustain) && note.WasHit
                ? note.DisjointMask
                : note.NoteMask;

            // If disjointed and is sustain logic (was hit), can hit if disjoint mask matches
            if ((note.IsDisjoint || note.IsExtendedSustain) && note.WasHit && (note.DisjointMask & buttonsMasked) != 0)
            {
                return true;
            }

            // If open, must not hold any frets
            // If not open, must be holding at least 1 fret
            if (noteMask == 0 && buttonsMasked != 0 || noteMask != 0 && buttonsMasked == 0)
            {
                return false;
            }

            // If holding exact note mask, can hit
            if (buttonsMasked == noteMask)
            {
                return true;
            }

            // Anchoring

            // XORing the two masks will give the anchor (held frets) around the note.
            int anchorButtons = buttonsMasked ^ noteMask;

            // Chord logic
            if (note.IsChord)
            {
                if (note.IsStrum)
                {
                    // Buttons must match note mask exactly for strum chords
                    return buttonsMasked == noteMask;
                }

                // Anchoring hopo/tap chords

                // Gets the lowest fret of the chord.
                var fretMask = 0;
                for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
                {
                    fretMask = 1 << (int)fret;

                    // If the current fret mask is part of the chord, break
                    if ((fretMask & note.NoteMask) == fretMask)
                    {
                        break;
                    }
                }

                // Anchor part:
                // Lowest fret of chord must be bigger or equal to anchor buttons
                // (can't hold note higher than the highest fret of chord)

                // Button mask subtract the anchor must equal chord mask (all frets of chord held)
                return fretMask >= anchorButtons && buttonsMasked - anchorButtons == note.NoteMask;
            }

            // Anchoring single notes

            // Anchor buttons held are lower than the note mask
            return anchorButtons < noteMask;
        }

        protected override bool HitNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;

            if (note.IsHopo || note.IsTap)
            {
                bool strumLeniencyActive = State.StrumLeniencyTimer.IsActive(State.CurrentTime);

                // Disallow hitting if front end timer is not in range of note time and didn't strum
                // (tried to hit as a hammeron/pulloff)
                // Also allows first note to be hit without infinite front end

                if (!EngineParameters.InfiniteFrontEnd && State.FrontEndTimer.IsExpired(note.Time) /* && !strumLeniencyActive */ && State.NoteIndex > 0)
                {
                    return false;
                }

                State.TapButtonMask = 0;

                // TODO Everything below here is timer stuff
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.TapButtonMask = 0;
                
                // Does the same thing but ensures it still works when infinite front end is disabled
                State.FrontEndTimer.Reset();

                State.WasHopoStrummed = false;
            }

            return base.HitNote(note);
        }
        
        protected override void MissNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;
            base.MissNote(note);
        }

        protected override void UpdateSustains()
        {
        }
        
        protected void UpdateInput()
        {
            if (!IsInputUpdate)
            {
                return;
            }

            if (IsStrumInput(CurrentInput))
            {
                State.StrummedThisUpdate = CurrentInput.Button;
            } else if (IsFretInput(CurrentInput))
            {
                State.LastButtonMask = State.ButtonMask;
                ToggleFret(CurrentInput.Action, CurrentInput.Button);
                State.FrontEndTimer.Start(State.CurrentTime);
                
                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "FrontEnd",
                    TimerStarted = true,
                    TimerValue = Math.Abs(EngineParameters.FrontEnd),
                });
            } else if (CurrentInput.GetAction<GuitarAction>() == GuitarAction.StarPower)
            {
                State.IsStarPowerInputActive = CurrentInput.Button;
            }
        }
        
        protected bool CheckForGhostInput(GuitarNote note)
        {
            // First note cannot be ghosted, nor can a note be ghosted if a button is unpressed (pulloff)
            if (note.PreviousNote is null || !CurrentInput.Button)
            {
                return false;
            }

            // Note can only be ghosted if it's in timing window
            if (!IsNoteInWindow(note))
            {
                return false;
            }

            // Input is a hammer-on if the highest fret held is higher than the highest fret of the previous mask
            bool isHammerOn = GetMostSignificantBit(State.ButtonMask) > GetMostSignificantBit(State.LastButtonMask);

            // Input is a hammer-on and the button pressed is not part of the note mask (incorrect fret)
            if(isHammerOn && (State.ButtonMask & note.NoteMask) == 0)
            {
                return true;
            }

            return false;
        }
        
        private static int GetMostSignificantBit(int mask)
        {
            // Gets the most significant bit of the mask
            var msbIndex = 0;
            while (mask != 0)
            {
                mask >>= 1;
                msbIndex++;
            }

            return msbIndex;
        }
    }
}