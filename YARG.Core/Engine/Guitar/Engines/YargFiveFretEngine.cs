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

            if (action is GuitarAction.StarPower && gameInput.Button && EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
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
            else if (action is GuitarAction.Whammy)
            {
                State.HasWhammied = true;
            }
        }

        protected override bool UpdateEngineLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateStarPower();
            UpdateSustains();

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                return false;
            }

            var note = Notes[State.NoteIndex];
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            // Overstrum for strum leniency
            if (State.StrumLeniencyTimer.IsExpired(State.CurrentTime))
            {
                Overstrum();
                State.StrumLeniencyTimer.Reset();
            }

            // Check for note miss note (back end)
            if (State.CurrentTime >= note.Time + EngineParameters.HitWindow.GetBackEnd(hitWindow))
            {
                if (!note.WasFullyHitOrMissed())
                {
                    MissNote(note);
                    return true;
                }
            }

            // Infinite front end hit (this can happen regardless of if infinite front end is on or not)
            if (State.InfiniteFrontEndHitTime is not null &&
                State.InfiniteFrontEndHitTime <= State.CurrentTime)
            {
                State.InfiniteFrontEndHitTime = null;

                var inputConsumed = ProcessNote(note, false);

                if (inputConsumed)
                {
                    // If an input was consumed, a note was hit
                    return true;
                }
            }

            // Check for strum hit
            if (State.HasStrummed)
            {
                State.HasStrummed = false;

                State.InfiniteFrontEndHitTime = null;

                var inputConsumed = ProcessNote(note, true);

                if (!inputConsumed)
                {
                    // If the input was NOT consumed, then attempt to overstrum
                    if (!State.HopoLeniencyTimer.IsActive(State.CurrentTime))
                    {
                        if (State.StrumLeniencyTimer.IsActive(State.CurrentTime))
                        {
                            // If the strum leniency timer was already active,
                            // that means that the player is already in the leniency.
                            Overstrum();
                            // ... then start the strum leniency timer for *this*
                            // strum.
                        }

                        // The engine will overstrum once this timer runs out
                        if (IsNoteInWindow(note))
                        {
                            // Use the normal leniency if there are notes in the hit window
                            State.StrumLeniencyTimer.Start(State.CurrentTime);
                        }
                        else
                        {
                            // Use small leniency if there are no notes in the hit window
                            State.StrumLeniencyTimer.StartWithOffset(State.CurrentTime,
                                EngineParameters.StrumLeniencySmall);
                        }
                    }
                    else
                    {
                        State.HopoLeniencyTimer.Reset();
                    }
                }
                else
                {
                    // If an input was consumed, a note was hit

                    // If the strum was successful and there strum leniency is active,
                    // then that means that there was an extra strum done.
                    if (State.StrumLeniencyTimer.IsActive(State.CurrentTime))
                    {
                        // If the strum leniency timer was already active,
                        // that means that the player is already in the leniency.
                        Overstrum();
                        State.StrumLeniencyTimer.Reset();
                    }

                    // Force infinite front end for the next note...
                    if (State.NoteIndex < Notes.Count)
                    {
                        var nextNote = Notes[State.NoteIndex];

                        // ...only if the current note is a strum
                        if (note.IsStrum)
                        {
                            double newHitWindow =
                                EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

                            CheckInfiniteFrontEndAndGhost(nextNote, newHitWindow);
                        }
                    }

                    return true;
                }
            }

            // Check for fret hit
            if (State.HasFretted)
            {
                State.HasFretted = false;

                State.InfiniteFrontEndHitTime = null;

                // If the strum leniency is active, act as this is a strum
                bool strummed = State.StrumLeniencyTimer.IsActive(State.CurrentTime);
                var inputConsumed = ProcessNote(note, strummed);

                if (!inputConsumed)
                {
                    // If strum leniency is active and the note was not hit,
                    // then wait until the timer runs out to overstrum

                    CheckInfiniteFrontEndAndGhost(note, hitWindow);
                }
                else
                {
                    // If the strum leniency is active and a note hit was successful,
                    // use up the strum leniency to prevent an extra overstrum.
                    if (strummed)
                    {
                        State.StrumLeniencyTimer.Reset();
                    }

                    return true;
                }
            }

            // Strum leniency hit
            if (State.StrumLeniencyTimer.IsActive(State.CurrentTime) &&
                State.CurrentTime >= note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // If the strum leniency timer is active, then attempt to hit as strum
                var strumConsumed = ProcessNote(note, true);

                if (strumConsumed)
                {
                    State.StrumLeniencyTimer.Reset();
                    return true;
                }
            }

            return false;
        }

        private void CheckInfiniteFrontEndAndGhost(GuitarNote note, double hitWindow)
        {
            // If the note *can* be hit with the current fret state, then
            // start the infinite front end
            if (EngineParameters.InfiniteFrontEnd && (note.IsHopo || note.IsTap) && CanNoteBeHit(note))
            {
                var hitTime = note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow);

                // If we're already past this point, then it wouldn't be an infinite front-end,
                // it'd just be a normal front-end.
                if (State.CurrentTime <= hitTime)
                {
                    State.InfiniteFrontEndHitTime = hitTime;
                    QueueUpdateTime(hitTime);
                }
            }

            // Don't ghost before the first note
            if (note.PreviousNote is not null)
            {
                bool ghosted = CheckForGhostInput(note);

                if (ghosted)
                {
                    EngineStats.GhostInputs++;

                    State.WasNoteGhosted = EngineParameters.AntiGhosting && ghosted;
                }
            }
        }

        private bool ProcessNote(GuitarNote note, bool strummed)
        {
            if (note.WasHit || note.WasMissed)
            {
                return false;
            }

            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // Pass on the input
                return false;
            }

            // If the note cannot be hit, try to note skip
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

                // Recursively call
                if (note.NextNote is not null)
                {
                    var inputConsumed = ProcessNote(note.NextNote, strummed);
                    return inputConsumed;
                }

                return false;
            }

            // At this point, CanNoteBeHit is true

            // Strum hit
            if (strummed)
            {
                HitNote(note);
                return true;
            }

            // HOPO hit
            var hopoAndHittable = note.IsHopo && EngineStats.Combo > 0;
            if ((note.IsTap || hopoAndHittable) && !State.WasNoteGhosted)
            {
                State.HopoLeniencyTimer.Start(State.CurrentTime);
                HitNote(note);
                return true;
            }

            // Pass on the input
            return false;
        }

        public override void UpdateBot(double songTime)
        {
            throw new System.NotImplementedException();
        }

        protected override bool CheckForNoteHit() => throw new System.NotImplementedException();

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