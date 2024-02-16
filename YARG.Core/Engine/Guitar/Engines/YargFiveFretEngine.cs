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
            if (action == GuitarAction.StarPower && gameInput.Button && EngineStats.CanStarPowerActivate)
            {
                ActivateStarPower();
                return;
            }

            // Strumming
            if (action is GuitarAction.StrumDown or GuitarAction.StrumUp && gameInput.Button)
            {
                State.DidStrum = true;
                return;
            }

            // Fretting
            if (IsFretInput(gameInput))
            {
                ToggleFret(gameInput.Action, gameInput.Button);
                State.DidFret = true;
                return;
            }
        }

        protected override bool UpdateEngineLogic(double time)
        {
            UpdateTimeVariables(time);
            UpdateStarPower();

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
                // Overstrum(); problem
                State.StrumLeniencyTimer.Reset();
            }

            // Check for note miss note (back end)
            if (State.CurrentTime > note.Time + EngineParameters.HitWindow.GetBackEnd(hitWindow))
            {
                if (!note.WasFullyHitOrMissed())
                {
                    MissNote(note);
                    return true;
                }
            }

            // Check for strum hit
            if (State.DidStrum)
            {
                var inputEaten = ProcessNoteStrum(note);

                if (!inputEaten)
                {
                    // If the input was NOT eaten, then attempt to overstrum
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
                        State.StrumLeniencyTimer.Start(State.CurrentTime);
                    }
                    else
                    {
                        State.HopoLeniencyTimer.Reset();
                    }

                    State.DidStrum = false;
                }
                else
                {
                    // If an input was eaten, a note was hit
                    State.DidStrum = false;
                    return true;
                }
            }

            // Check for fret hit
            if (State.DidFret)
            {
                if (State.StrumLeniencyTimer.IsActive(State.CurrentTime))
                {
                    // If the strum leniency timer is active, then attempt to hit a strum

                    var strumEaten = ProcessNoteStrum(note);

                    if (strumEaten)
                    {
                        State.StrumLeniencyTimer.Reset();

                        State.DidFret = false;
                        return true;
                    }

                    // ... otherwise attempt to hit a tap
                }

                var inputEaten = ProcessNoteTap(note);

                if (!inputEaten)
                {
                    // TODO: Ghost

                    State.DidFret = false;
                }
                else
                {
                    State.DidFret = false;
                    return true;
                }
            }

            return false;
        }

        private bool ProcessNoteStrum(GuitarNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // Pass on the input
                return false;
            }

            if (CanNoteBeHit(note))
            {
                HitNote(note);
                return true;
            }

            // Pass on the input
            return false;
        }

        private bool ProcessNoteTap(GuitarNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            if (!EngineParameters.InfiniteFrontEnd &&
                State.CurrentTime < note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow))
            {
                // Pass on the input
                return false;
            }

            if (CanNoteBeHit(note))
            {
                if (note.IsTap || (note.IsHopo && EngineStats.Combo > 0))
                {
                    HitNote(note);

                    State.HopoLeniencyTimer.Start(State.CurrentTime);

                    return true;
                }
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
            // foreach (var sustain in ActiveSustains)
            // {
            //     var sustainNote = sustain.Note;
            //
            //     // Don't want to mask off the note we're checking otherwise it'll always return false lol
            //     if (note == sustainNote)
            //     {
            //         continue;
            //     }
            //
            //     // Mask off the disjoint mask if its disjointed or extended disjointed
            //     // This removes just the single fret of the disjoint note
            //     if ((sustainNote.IsExtendedSustain && sustainNote.IsDisjoint) || sustainNote.IsDisjoint)
            //     {
            //         buttonsMasked -= (byte) sustainNote.DisjointMask;
            //     }
            //     else if (sustainNote.IsExtendedSustain)
            //     {
            //         // Remove the entire note mask if its an extended sustain
            //         // Difference between NoteMask and DisjointMask is that DisjointMask is only a single fret
            //         // while NoteMask is the entire chord
            //         buttonsMasked -= (byte) sustainNote.NoteMask;
            //     }
            // }

            bool disjointOrExtended = note.IsDisjoint || note.IsExtendedSustain;

            // Use the DisjointMask for comparison if disjointed and was hit (for sustain logic)
            int noteMask = disjointOrExtended && note.WasHit
                ? note.DisjointMask
                : note.NoteMask;

            // If disjointed and is sustain logic (was hit), can hit if disjoint mask matches
            if (disjointOrExtended && note.WasHit && (note.DisjointMask & fretMask) != 0)
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
                int chordMask = 0;
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
    }
}