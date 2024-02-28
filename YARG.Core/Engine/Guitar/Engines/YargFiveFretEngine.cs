using System;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<GuitarAction>();

            // Star power
            if (action is GuitarAction.StarPower && gameInput.Button && EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
            }
            else if (action is GuitarAction.Whammy)
            {
                State.HasWhammied = true;
            }
            else if (action is GuitarAction.StrumDown or GuitarAction.StrumUp && gameInput.Button)
            {
                State.HasStrummed = true;
            }
            else if (IsFretInput(gameInput))
            {
                State.LastFretMask = State.FretMask;
                State.HasFretted = true;
                State.IsFretPress = gameInput.Button;

                ToggleFret(gameInput.Action, gameInput.Button);
            }
        }

        protected override bool UpdateEngineLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateStarPower();
            UpdateTimers();

            // This is up here so overstrumming still works when there are no notes left
            if (State.HasStrummed)
            {
                // Hopo was hit recently, eat strum input
                if (State.HopoLeniencyTimer.IsEnabled)
                {
                    State.StrumLeniencyTimer.Disable();

                    // Disable hopo leniency as hopos can only eat one strum
                    State.HopoLeniencyTimer.Disable();
                }
                else
                {
                    // Strummed while strum leniency is active (double strum)
                    if (State.StrumLeniencyTimer.IsEnabled)
                    {
                        Overstrum();
                    }
                }
            }

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                UpdateSustains();
                State.HasStrummed = false;
                State.HasFretted = false;
                State.IsFretPress = false;
                State.HasWhammied = false;
                return false;
            }

            var note = Notes[State.NoteIndex];
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (State.HasStrummed)
            {
                // Offset timer by small strum leniency if there's no note in the hit window
                double offset = IsNoteInWindow(note) ? EngineParameters.StrumLeniencySmall : 0;

                // Start the strum leniency timer at full value
                StartTimer(ref State.StrumLeniencyTimer, State.CurrentTime, offset);
            }

            if (State.HasFretted)
            {
                State.HasTapped = true;

                var frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

                // This is the time the front end will expire. Used for hit logic with infinite front end
                State.FrontEndExpireTime = State.CurrentTime + Math.Abs(frontEnd);

                // This is the expected hit time of the note with infinite front end. Engine will update at this time
                AddConsistencyAnchor(note.Time + frontEnd);

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

            State.HasStrummed = false;
            State.HasFretted = false;
            State.IsFretPress = false;
            State.HasWhammied = false;
            return isNoteHit;
        }

        public override void UpdateBot(double songTime)
        {
            throw new NotImplementedException();
        }

        protected override bool CheckForNoteHit()
        {
            var note = Notes[State.NoteIndex];
            double fullWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (note.WasFullyHitOrMissed())
            {
                return false;
            }

            // Note not in window
            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(fullWindow))
            {
                return false;
            }

            // Check for note miss note (back end)
            if (State.CurrentTime > note.Time + EngineParameters.HitWindow.GetBackEnd(fullWindow))
            {
                MissNote(note);
                return true;
            }

            // Note skipping, useful for combo regain
            if (!CanNoteBeHit(note))
            {
                // TODO Add note skipping logic
                return false;
            }

            // Handles hitting a hopo/tap notes
            // If first note is a hopo then it can be hit without combo (for practice mode)
            bool hopoCondition = note.IsHopo && (EngineStats.Combo > 0 || State.NoteIndex == 0);
            if (State.HasTapped && (hopoCondition || note.IsTap) && !State.WasNoteGhosted)
            {
                // Disallow hitting if front end timer is not in range of note time and didn't strum
                // (tried to hit as a hammeron/pulloff)
                // Also allows first note to be hit without infinite front end

                bool frontEndExpired = note.Time > State.FrontEndExpireTime;

                // Try to hit with infinite front end
                if (!EngineParameters.InfiniteFrontEnd && frontEndExpired &&
                    State.NoteIndex > 0)
                {
                    return false;
                }

                HitNote(note);
                return true;
            }

            // If hopo/tap checks failed then the note can be hit if it was strummed
            if (State.HasStrummed || State.StrumLeniencyTimer.IsEnabled)
            {
                return HitNote(note);
            }

            return false;
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            byte fretMask = State.FretMask;
            foreach (var sustain in ActiveSustains)
            {
                var sustainNote = sustain.Note;

                // Don't want to mask off the note we're checking otherwise it'll always return false lol
                if (note == sustainNote)
                {
                    continue;
                }

                // Mask off the disjoint mask if its disjointed or extended disjointed
                // This removes just the single fret of the disjoint note
                if ((sustainNote.IsExtendedSustain && sustainNote.IsDisjoint) || sustainNote.IsDisjoint)
                {
                    fretMask -= (byte) sustainNote.DisjointMask;
                }
                else if (sustainNote.IsExtendedSustain)
                {
                    // Remove the entire note mask if its an extended sustain
                    // Difference between NoteMask and DisjointMask is that DisjointMask is only a single fret
                    // while NoteMask is the entire chord
                    fretMask -= (byte) sustainNote.NoteMask;
                }
            }

            // Only used for sustain logic
            bool useDisjointMask = note is { IsDisjoint: true, WasHit: true };

            // Use the DisjointMask for comparison if disjointed and was hit (for sustain logic)
            int noteMask = useDisjointMask ? note.DisjointMask : note.NoteMask;

            // If disjointed and is sustain logic (was hit), can hit if disjoint mask matches
            if (useDisjointMask && (note.DisjointMask & fretMask) != 0)
            {
                return true;
            }

            // If open, must not hold any frets
            // If not open, must be holding at least 1 fret
            if (noteMask == 0 && fretMask != 0 || noteMask != 0 && fretMask == 0)
            {
                return false;
            }

            // If holding exact note mask, can hit
            if (fretMask == noteMask)
            {
                return true;
            }

            // Anchoring

            // XORing the two masks will give the anchor (held frets) around the note.
            int anchorButtons = fretMask ^ noteMask;

            // Chord logic
            if (note.IsChord)
            {
                if (note.IsStrum)
                {
                    // Buttons must match note mask exactly for strum chords
                    return fretMask == noteMask;
                }

                // Anchoring hopo/tap chords

                // Gets the lowest fret of the chord.
                var chordMask = 0;
                for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
                {
                    chordMask = 1 << (int) fret;

                    // If the current fret mask is part of the chord, break
                    if ((chordMask & note.NoteMask) == chordMask)
                    {
                        break;
                    }
                }

                // Anchor part:
                // Lowest fret of chord must be bigger or equal to anchor buttons
                // (can't hold note higher than the highest fret of chord)

                // Button mask subtract the anchor must equal chord mask (all frets of chord held)
                return chordMask >= anchorButtons && fretMask - anchorButtons == note.NoteMask;
            }

            // Anchoring single notes

            // Anchor buttons held are lower than the note mask
            return anchorButtons < noteMask;
        }

        protected override bool HitNote(GuitarNote note)
        {
            State.HasTapped = false;

            if (note.IsHopo || note.IsTap)
            {
                bool strumLeniencyActive = State.StrumLeniencyTimer.IsEnabled;

                // Note can hit now

                State.HopoLeniencyTimer.Start(State.CurrentTime);

                // Strummed a tap, or hopo while in combo
                if (((note.IsHopo && EngineStats.Combo > 0) || note.IsTap) && strumLeniencyActive)
                {
                    StartTimer(ref State.StrumLeniencyTimer, State.CurrentTime, EngineParameters.StrumLeniencySmall);
                }
                else
                {
                    // Strummed a strum, or hopo with no combo
                    State.StrumLeniencyTimer.Disable();
                }
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.HasTapped = true;

                // Does the same thing but ensures it still works when infinite front end is disabled
                EngineTimer.Reset(ref State.FrontEndExpireTime);

                State.StrumLeniencyTimer.Disable();
            }

            return base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            State.HasTapped = false;
            base.MissNote(note);
        }

        protected void UpdateTimers()
        {
            if (State.StrumLeniencyTimer.IsEnabled && State.StrumLeniencyTimer.IsExpired(State.CurrentTime))
            {
                Overstrum();
                State.StrumLeniencyTimer.Disable();
            }
        }

        protected bool CheckForGhostInput(GuitarNote note)
        {
            // First note cannot be ghosted, nor can a note be ghosted if a button is unpressed (pulloff)
            if (note.PreviousNote is null || !State.IsFretPress)
            {
                return false;
            }

            // Note can only be ghosted if it's in timing window
            if (!IsNoteInWindow(note))
            {
                return false;
            }

            // Input is a hammer-on if the highest fret held is higher than the highest fret of the previous mask
            bool isHammerOn = GetMostSignificantBit(State.FretMask) > GetMostSignificantBit(State.LastFretMask);

            // Input is a hammer-on and the button pressed is not part of the note mask (incorrect fret)
            if (isHammerOn && (State.FretMask & note.NoteMask) == 0)
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