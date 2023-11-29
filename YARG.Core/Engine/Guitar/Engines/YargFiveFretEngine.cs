using System;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
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
                State.FrontEndStartTime = note.Time;

                foreach (var sustainNote in ActiveSustains)
                {
                    if (sustainNote.IsDisjoint)
                    {
                        State.ButtonMask |= (byte) sustainNote.DisjointMask;
                    }
                    else
                    {
                        State.ButtonMask |= (byte) sustainNote.NoteMask;
                    }
                }

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
            UpdateTimeVariables(time);
            UpdateTimers();

            if (IsInputUpdate && CurrentInput.GetAction<GuitarAction>() == GuitarAction.StarPower &&
                EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
            }

            DepleteStarPower(GetUsedStarPower());

            State.StrummedThisUpdate = (IsInputUpdate && IsStrumInput(CurrentInput) && CurrentInput.Button)
                || (State.StrummedThisUpdate && IsBotUpdate);

            bool isFretInput = IsInputUpdate && IsFretInput(CurrentInput);

            if (isFretInput)
            {
                State.LastButtonMask = State.ButtonMask;
                ToggleFret(CurrentInput.Action, CurrentInput.Button);
                State.FrontEndStartTime = State.CurrentTime;
            }

            if (State.ButtonMask != State.TapButtonMask)
            {
                State.TapButtonMask = 0;
            }

            // This is up here so overstrumming still works when there are no notes left
            if (State.StrummedThisUpdate)
            {
                // Strummed while strum leniency active
                if (State.StrumLeniencyTimer.IsActive(State.CurrentTime))
                {
                    Overstrum();
                }

                // Use small leniency (in case no notes in window or last note)
                State.StrumLeniencyTimer.StartWithOffset(State.CurrentTime, EngineParameters.StrumLeniencySmall);

                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "StrumLeniency",
                    TimerStarted = true,
                    TimerValue = EngineParameters.StrumLeniencySmall,
                });
            }

            // Quits early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                UpdateSustains();
                return false;
            }

            var note = Notes[State.NoteIndex];

            // Set strum leniency to full leniency time if not is in window
            if (State.StrummedThisUpdate && IsNoteInWindow(note))
            {
                State.StrumLeniencyTimer.Start(State.CurrentTime);

                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "StrumLeniency",
                    TimerStarted = true,
                    TimerValue = EngineParameters.StrumLeniency,
                });
            }

            if (isFretInput)
            {
                // Check for fret ghosting
                // We want to run ghost logic regardless of the setting for the ghost counter
                if (note.PreviousNote is not null)
                {
                    bool ghosted = CheckForGhostInput(note);

                    // This variable controls hit logic for ghosting
                    State.WasNoteGhosted = EngineParameters.AntiGhosting && (ghosted || State.WasNoteGhosted);

                    // Add ghost inputs to stats regardless of the setting for anti ghosting
                    if (ghosted)
                    {
                        EngineStats.GhostInputs++;
                    }
                }
            }

            bool isNoteHit = CheckForNoteHit();

            UpdateSustains();
            return isNoteHit;
        }

        protected void UpdateTimers()
        {
            // We need to check if the strum leniency was active prior to this update
            // Then further down, we check if it expires on THIS update (if it does, we overstrum)
            if (State.StrumLeniencyTimer.IsActive(State.LastUpdateTime))
            {
                // A hopo was strummed recently
                if (State.HopoLeniencyTimer.IsActive(State.CurrentTime))
                {
                    // // Hopo was double strummed, overstrum
                    // if (State.WasHopoStrummed)
                    // {
                    //     YargTrace.LogInfo("Hopo was double strummed. Overstrumming.");
                    //     Overstrum();
                    //     State.WasHopoStrummed = false;
                    // }
                    // else
                    // {
                    //     YargTrace.LogInfo("Hopo/tap was strummed");
                    //     State.WasHopoStrummed = true;
                    // }

                    //YargTrace.LogInfo("Hopo ate strum input");

                    // This eats the strum input
                    State.StrumLeniencyTimer.Reset();
                    State.HopoLeniencyTimer.Reset();
                }
                else
                {
                    // Strum leniency expires on this update, overstrum
                    if (State.StrumLeniencyTimer.IsExpired(State.CurrentTime))
                    {
                        if (State.WasHopoStrummed)
                        {
                            State.StrumLeniencyTimer.Reset();
                        }
                        else
                        {
                            //YargTrace.LogInfo($"Hopo leniency: {State.CurrentTime - State.HopoLeniencyStartTime}");
                            //YargTrace.LogInfo("Strum leniency ran out, overstrumming");
                            Overstrum();
                        }

                        State.WasHopoStrummed = false;
                    }
                }
            }
        }

        protected override bool CheckForNoteHit()
        {
            var note = Notes[State.NoteIndex];
            double fullWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (note.WasHit || note.WasMissed)
            {
                return false;
            }

            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(fullWindow))
            {
                return false;
            }

            if (State.CurrentTime > note.Time + EngineParameters.HitWindow.GetBackEnd(fullWindow) && !note.WasHit)
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

                // Skipping hopos or taps not allowed if its the first note
                if ((note.IsHopo || note.IsTap) && State.NoteIndex == 0)
                {
                    return false;
                }

                var next = note.NextNote;
                while (next is not null)
                {
                    double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(next));
                    if (State.CurrentTime < next.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
                    {
                        return false;
                    }

                    // Don't need to check back end because if we're here then the previous note was not out of time

                    if (CanNoteBeHit(next) &&
                        (State.StrummedThisUpdate || State.StrumLeniencyTimer.IsActive(State.CurrentTime) || next.IsTap)
                        && State.TapButtonMask == 0)
                    {
                        if (HitNote(next))
                        {
                            //YargTrace.LogInfo($"Skipping to hit next note as it is hittable ({State.TapButtonMask})");
                            return true;
                        }
                    }

                    next = next.NextNote;
                }

                return false;
            }

            // Handles hitting a hopo/tap notes
            // If first note is a hopo then it can be hit without combo (for practice mode)
            bool hopoCondition = note.IsHopo && (EngineStats.Combo > 0 || State.NoteIndex == 0);
            if (State.TapButtonMask == 0 && (hopoCondition || note.IsTap) && !State.WasNoteGhosted)
            {
                return HitNote(note);
            }

            // If hopo/tap checks failed then the note can be hit if it was strummed
            if (State.StrummedThisUpdate || State.StrumLeniencyTimer.IsActive(State.CurrentTime))
            {
                return HitNote(note);
            }

            return false;
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

        protected override void UpdateSustains()
        {
            // No sustains
            if (ActiveSustains.Count == 0)
            {
                return;
            }

            bool isStarPowerSustainActive = false;
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var note = ActiveSustains[i];

                isStarPowerSustainActive = note.IsStarPower || isStarPowerSustainActive;
                bool sustainEnded = State.CurrentTick > note.TickEnd;

                if (!CanNoteBeHit(note) || sustainEnded)
                {
                    ActiveSustains.Remove(note);
                    i--;
                    OnSustainEnd?.Invoke(note, State.CurrentTime);
                }
            }

            if (isStarPowerSustainActive && CurrentInput.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                EngineStats.StarPowerAmount += (State.CurrentTick - State.LastTick) / (double)State.TicksEveryEightMeasures;
                if (EngineStats.StarPowerAmount >= 1)
                {
                    EngineStats.StarPowerAmount = 1;
                }
            }
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

                double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
                double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

                double frontEndAbs = Math.Abs(frontEnd);
                bool frontEndExpired = EngineTimer.IsExpired(State.FrontEndStartTime, note.Time, frontEndAbs);
                if (!EngineParameters.InfiniteFrontEnd && frontEndExpired && !strumLeniencyActive && State.NoteIndex > 0)
                {
                    return false;
                }

                // Strummed a tap, or hopo while in combo
                if (((note.IsHopo && EngineStats.Combo > 0) || note.IsTap) && strumLeniencyActive)
                {
                    State.StrumLeniencyTimer.StartWithOffset(State.CurrentTime, EngineParameters.StrumLeniencySmall);

                    EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                    {
                        TimerName = "StrumLeniency",
                        TimerStarted = true,
                        TimerValue = EngineParameters.StrumLeniencySmall
                    });
                    State.WasHopoStrummed = true;
                }
                else
                {
                    State.StrumLeniencyTimer.Reset();

                    EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                    {
                        TimerName = "StrumLeniency",
                        TimerStopped = true,
                        TimerValue = 0
                    });
                }

                State.HopoLeniencyTimer.Start(State.CurrentTime);

                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "HopoLeniency",
                    TimerStarted = true,
                    TimerValue = Math.Abs(EngineParameters.HopoLeniency)
                });
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.TapButtonMask = 0;

                // Does the same thing but ensures it still works when infinite front end is disabled
                EngineTimer.Reset(ref State.FrontEndStartTime);

                State.WasHopoStrummed = false;

                State.StrumLeniencyTimer.Reset();

                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "StrumLeniency",
                    TimerStopped = true,
                    TimerValue = 0
                });
            }

            return base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            State.TapButtonMask = State.ButtonMask;
            base.MissNote(note);
        }

        private int GetMostSignificantBit(int mask)
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