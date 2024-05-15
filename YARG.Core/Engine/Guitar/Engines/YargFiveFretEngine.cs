using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
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

            State.LastButtonMask = State.ButtonMask;
            State.ButtonMask = (byte) note.NoteMask;

            YargLogger.LogFormatTrace("[Bot] Set button mask to: {0}", State.ButtonMask);

            State.HasTapped = State.ButtonMask != State.LastButtonMask;
            State.IsFretPress = true;
            State.HasStrummed = false;
            State.StrumLeniencyTimer.Start(time);

            foreach (var sustain in ActiveSustains)
            {
                var sustainNote = sustain.Note;

                if (!sustainNote.IsExtendedSustain)
                {
                    continue;
                }

                if (sustainNote.IsDisjoint)
                {
                    State.ButtonMask |= (byte) sustainNote.DisjointMask;

                    YargLogger.LogFormatTrace("[Bot] Added Disjoint Sustain Mask {0} to button mask. {1}", sustainNote.DisjointMask, State.ButtonMask);
                }
                else
                {
                    State.ButtonMask |= (byte) sustainNote.NoteMask;

                    YargLogger.LogFormatTrace("[Bot] Added Sustain Mask {0} to button mask. {1}", sustainNote.NoteMask, State.ButtonMask);
                }
            }
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<GuitarAction>();

            // Star power
            if (action is GuitarAction.StarPower)
            {
                State.IsStarPowerInputActive = gameInput.Button;
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
                State.LastButtonMask = State.ButtonMask;
                State.HasFretted = true;
                State.IsFretPress = gameInput.Button;

                ToggleFret(gameInput.Action, gameInput.Button);
            }

            YargLogger.LogFormatTrace("Mutated input state: Button Mask: {0}, HasFretted: {1}, HasStrummed: {2}",
                State.ButtonMask, State.HasFretted, State.HasStrummed);
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();
            UpdateTimers();

            bool strumEatenByHopo = false;

            // This is up here so overstrumming still works when there are no notes left
            if (State.HasStrummed)
            {
                // Hopo was hit recently, eat strum input
                if (State.HopoLeniencyTimer.IsActive)
                {
                    State.StrumLeniencyTimer.Disable();

                    // Disable hopo leniency as hopos can only eat one strum
                    State.HopoLeniencyTimer.Disable();

                    strumEatenByHopo = true;
                    ReRunHitLogic = true;
                }
                else
                {
                    // Strummed while strum leniency is active (double strum)
                    if (State.StrumLeniencyTimer.IsActive)
                    {
                        Overstrum();
                    }
                }
            }

            // First update all the active sustains (ones which were started before this update)
            // This can drop any sustains which end at this time
            // UpdateSustains(false);

            // Update bot (will return if not enabled)
            UpdateBot(time);

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                State.HasStrummed = false;
                State.HasFretted = false;
                State.IsFretPress = false;
                State.HasWhammied = false;
                return;
            }

            var note = Notes[State.NoteIndex];

            var hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            var frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

            if (State.HasStrummed && !strumEatenByHopo)
            {
                // Offset timer by small strum leniency if there's no note in the hit window
                double offset = !IsNoteInWindow(note) ? EngineParameters.StrumLeniencySmall : 0;

                // Start the strum leniency timer at full value
                StartTimer(ref State.StrumLeniencyTimer, State.CurrentTime, offset);

                ReRunHitLogic = true;
            }

            if (State.HasFretted)
            {
                State.HasTapped = true;

                // This is the time the front end will expire. Used for hit logic with infinite front end
                State.FrontEndExpireTime = State.CurrentTime + Math.Abs(frontEnd);

                // Check for fret ghosting
                // We want to run ghost logic regardless of the setting for the ghost counter
                bool ghosted = CheckForGhostInput(note);

                // This variable controls hit logic for ghosting
                State.WasNoteGhosted = EngineParameters.AntiGhosting && (ghosted || State.WasNoteGhosted);

                // Add ghost inputs to stats regardless of the setting for anti ghosting
                if (ghosted)
                {
                    EngineStats.GhostInputs++;
                }
            }

            CheckForNoteHit();
            UpdateSustains();

            State.HasStrummed = false;
            State.HasFretted = false;
            State.IsFretPress = false;
            State.HasWhammied = false;
        }

        protected override void CheckForNoteHit()
        {
            for (int i = State.NoteIndex; i < Notes.Count; i++)
            {
                bool isFirstNoteInWindow = i == State.NoteIndex;
                var note = Notes[i];

                if (note.WasFullyHitOrMissed())
                {
                    break;
                }

                if (!IsNoteInWindow(note, out bool missed))
                {
                    if (missed)
                    {
                        MissNote(note);
                        YargLogger.LogFormatTrace("Missed note (Index: {0}, Mask: {1}) at {2}", i,
                            note.NoteMask, State.CurrentTime);
                    }

                    break;
                }

                // Cannot hit the note
                if (!CanNoteBeHit(note))
                {
                    YargLogger.LogFormatTrace("Cant hit note (Index: {0}, Mask {1}) at {2}. Buttons: {3}", i,
                        note.NoteMask, State.CurrentTime, State.ButtonMask);
                    // This does nothing special, it's just logging strum leniency
                    if (isFirstNoteInWindow && State is { HasStrummed: true, StrumLeniencyTimer: { IsActive: true } })
                    {
                        YargLogger.LogFormatTrace("Starting strum leniency at {0}, will end at {1}", State.CurrentTime,
                            State.StrumLeniencyTimer.EndTime);
                    }

                    // Note skipping not allowed on the first note if hopo/tap
                    if ((note.IsHopo || note.IsTap) && State.NoteIndex == 0)
                    {
                        break;
                    }

                    // Continue to the next note (skipping the current one)
                    continue;
                }

                // Handles hitting a hopo notes
                // If first note is a hopo then it can be hit without combo (for practice mode)
                bool hopoCondition = note.IsHopo && isFirstNoteInWindow &&
                    (EngineStats.Combo > 0 || State.NoteIndex == 0);

                // If a note is a tap then it can be hit only if it is the closest note, unless
                // the combo is 0 then it can be hit regardless of the distance (note skipping)
                bool tapCondition = note.IsTap && (isFirstNoteInWindow || EngineStats.Combo == 0);

                bool frontEndIsExpired = note.Time > State.FrontEndExpireTime;
                bool canUseInfFrontEnd =
                    EngineParameters.InfiniteFrontEnd || !frontEndIsExpired || State.NoteIndex == 0;

                // Attempt to hit with hopo/tap rules
                if (State.HasTapped && (hopoCondition || tapCondition) && canUseInfFrontEnd && !State.WasNoteGhosted)
                {
                    HitNote(note);
                    YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with hopo rules",
                        i, note.NoteMask, State.CurrentTime);
                    break;
                }

                // If hopo/tap checks failed then the note can be hit if it was strummed
                if ((State.HasStrummed || State.StrumLeniencyTimer.IsActive) &&
                    (isFirstNoteInWindow || (State.NoteIndex > 0 && EngineStats.Combo == 0)))
                {
                    HitNote(note);
                    if (State.HasStrummed)
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with strum input",
                            i, note.NoteMask, State.CurrentTime);
                    }
                    else
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with strum leniency",
                            i, note.NoteMask, State.CurrentTime);
                    }

                    break;
                }
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            byte buttonsMasked = State.ButtonMask;
            foreach (var sustain in ActiveSustains)
            {
                var sustainNote = sustain.Note;

                if (sustainNote.IsExtendedSustain)
                {
                    // Remove the note mask if its an extended sustain
                    // Difference between NoteMask and DisjointMask is that DisjointMask is only a single fret
                    // while NoteMask is the entire chord

                    var maskToRemove = sustainNote.IsDisjoint ? sustainNote.DisjointMask : sustainNote.NoteMask;
                    buttonsMasked -= (byte) maskToRemove;
                }
            }

            return IsNoteHittable(note, buttonsMasked);

            static bool IsNoteHittable(GuitarNote note, byte buttonsMasked)
            {
                // Only used for sustain logic
                bool useDisjointMask = note is { IsDisjoint: true, WasHit: true };

                // Use the DisjointMask for comparison if disjointed and was hit (for sustain logic)
                int noteMask = useDisjointMask ? note.DisjointMask : note.NoteMask;

                // If disjointed and is sustain logic (was hit), can hit if disjoint mask matches
                if (useDisjointMask && (note.DisjointMask & buttonsMasked) != 0)
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
                    return chordMask >= anchorButtons && buttonsMasked - anchorButtons == note.NoteMask;
                }

                // Anchoring single notes

                // Anchor buttons held are lower than the note mask
                return anchorButtons < noteMask;
            }
        }

        protected override void HitNote(GuitarNote note)
        {
            if (note.IsHopo || note.IsTap)
            {
                State.HasTapped = false;
                StartTimer(ref State.HopoLeniencyTimer, State.CurrentTime);
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                State.HasTapped = true;

                // Does the same thing but ensures it still works when infinite front end is disabled
                EngineTimer.Reset(ref State.FrontEndExpireTime);
            }

            State.StrumLeniencyTimer.Disable();

            base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            State.HasTapped = false;
            base.MissNote(note);
        }

        protected void UpdateTimers()
        {
            if (State.HopoLeniencyTimer.IsActive && State.HopoLeniencyTimer.IsExpired(State.CurrentTime))
            {
                State.HopoLeniencyTimer.Disable();

                ReRunHitLogic = true;
            }

            if (State.StrumLeniencyTimer.IsActive)
            {
                //YargTrace.LogInfo("Strum Leniency: Enabled");
                if (State.StrumLeniencyTimer.IsExpired(State.CurrentTime))
                {
                    //YargTrace.LogInfo("Strum Leniency: Expired. Overstrumming");
                    Overstrum();
                    State.StrumLeniencyTimer.Disable();

                    ReRunHitLogic = true;
                }
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
            bool isHammerOn = GetMostSignificantBit(State.ButtonMask) > GetMostSignificantBit(State.LastButtonMask);

            // Input is a hammer-on and the button pressed is not part of the note mask (incorrect fret)
            if (isHammerOn && (State.ButtonMask & note.NoteMask) == 0)
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
