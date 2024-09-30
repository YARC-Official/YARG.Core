using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProGuitar.Engines
{
    public class YargProGuitarEngine : ProGuitarEngine
    {
        public YargProGuitarEngine(InstrumentDifficulty<ProGuitarNote> chart, SyncTrack syncTrack,
            ProGuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void UpdateBot(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<ProGuitarAction>();

            if (action is ProGuitarAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
            else if (action <= ProGuitarAction.String6_Fret)
            {
                // This handles unpressing frets as well
                HeldFrets[(int) action] = (byte) gameInput.Integer;
                HasFretted = true;
            }
            else if (action is >= ProGuitarAction.String1_Strum and <= ProGuitarAction.String6_Strum)
            {
                if (gameInput.Button)
                {
                    Strums |= (byte) (1 << (action - ProGuitarAction.String1_Strum));

                    // TODO: The problem right now is that if you hit another string after the required ones, it counts as an overstrum

                    if (!ChordStrumLeniencyTimer.IsActive)
                    {
                        ChordStrumLeniencyTimer.Start(gameInput.Time);
                    }
                }
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();
            UpdateTimers();

            // Update bot (will return if not enabled)
            // UpdateBot(time);

            // Quit early if there are no notes left
            if (NoteIndex >= Notes.Count)
            {
                Strums = 0;
                UpdateSustains();
                return;
            }

            if (HasFretted)
            {
                HasTapped = true;

                // TODO: Ghosting
            }

            CheckForNoteHit();
            UpdateSustains();

            // "Strums" isn't reset here because the chord strum leniency will deal with it
            HasFretted = false;
        }

        protected void UpdateTimers()
        {
            if (AfterStrumLeniencyTimer.IsActive && AfterStrumLeniencyTimer.IsExpired(CurrentTime))
            {
                AfterStrumLeniencyTimer.Disable();
            }

            if (ChordStrumLeniencyTimer.IsActive && ChordStrumLeniencyTimer.IsExpired(CurrentTime))
            {
                Strums = 0;

                // Right after we hit a note...
                if (AfterStrumLeniencyTimer.IsActive)
                {
                    // Any string strummed is an extra strum (since all strings are required to hit a note)
                    for (int i = 0; i < 6; i++)
                    {
                        bool strummedString = ((Strums >> i) & 1) == 1;
                        if (strummedString)
                        {
                            ExtraStringsHit++;
                        }
                    }

                    // If the extra strings hit is over the limit, overstrum.
                    // Remember however, that there should only be one overstrum per extra strings hit per note.
                    if (ExtraStringsHit > EXTRA_STRING_HIT_LIMIT && !ExtraStringOverstrum)
                    {
                        Overstrum();
                        ExtraStringOverstrum = true;
                    }
                }
                else
                {
                    // We overstrum here because it means that the strum wasn't used up at this point
                    Overstrum();
                }

                ChordStrumLeniencyTimer.Disable();
            }
        }

        protected override void CheckForNoteHit()
        {
            for (int i = NoteIndex; i < Notes.Count; i++)
            {
                bool isFirstNoteInWindow = i == NoteIndex;
                var note = Notes[i];

                if (note.WasFullyHitOrMissed())
                {
                    break;
                }

                if (!IsNoteInWindow(note, out bool missed))
                {
                    if (isFirstNoteInWindow && missed)
                    {
                        MissNote(note);
                        YargLogger.LogFormatTrace("Missed note (Index: {0}) at {2}", i, CurrentTime);
                    }

                    break;
                }

                // Cannot hit the note
                if (!CanNoteBeHit(note))
                {
                    YargLogger.LogFormatTrace("Cant hit note (Index: {0}) at {1}", i, CurrentTime);

                    // Note skipping not allowed on the first note if hopo/tap
                    if ((note.IsHopo || note.IsTap) && NoteIndex == 0)
                    {
                        break;
                    }

                    // Continue to the next note (skipping the current one)
                    continue;
                }

                // TODO: Check what strings were strummed

                // Handles hitting a hopo notes
                // If first note is a hopo then it can be hit without combo (for practice mode)
                bool hopoCondition = note.IsHopo && isFirstNoteInWindow &&
                    (EngineStats.Combo > 0 || NoteIndex == 0);

                // If a note is a tap then it can be hit only if it is the closest note, unless
                // the combo is 0 then it can be hit regardless of the distance (note skipping)
                bool tapCondition = note.IsTap && (isFirstNoteInWindow || EngineStats.Combo == 0);

                // TODO: Infinite front end
                // bool frontEndIsExpired = note.Time > FrontEndExpireTime;
                // bool canUseInfFrontEnd =
                //     EngineParameters.InfiniteFrontEnd || !frontEndIsExpired || NoteIndex == 0;

                // Attempt to hit with hopo/tap rules
                if (HasTapped && (hopoCondition || tapCondition) && !WasNoteGhosted)
                {
                    HitNote(note);

                    AfterStrumLeniencyTimer.Disable();

                    YargLogger.LogFormatTrace("Hit note (Index: {0}) at {1} with hopo rules",
                        i, CurrentTime);
                    break;
                }

                // If hopo/tap checks failed then the note can be hit if it was strummed
                if (IsCorrectStrum(note.ChordMask) &&
                    (isFirstNoteInWindow || (NoteIndex > 0 && EngineStats.Combo == 0)))
                {
                    HitNote(note);

                    if (Strums != 0)
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}) at {1} with strum",
                            i, CurrentTime);
                    }

                    CountExtraStringsHit(note.ChordMask);
                    if (ExtraStringsHit > EXTRA_STRING_HIT_LIMIT)
                    {
                        ExtraStringOverstrum = true;
                        Overstrum();
                    }

                    // Make sure to reset strums
                    Strums = 0;
                    ChordStrumLeniencyTimer.Disable();

                    // Start the after strum timer
                    AfterStrumLeniencyTimer.Start(CurrentTime);

                    break;
                }
            }
        }

        protected bool IsCorrectStrum(FretBytes chordMask)
        {
            for (int i = 0; i < 6; i++)
            {
                bool strummedString = ((Strums >> i) & 1) == 1;

                if (chordMask[i] != FretBytes.IGNORE_BYTE && !strummedString)
                {
                    return false;
                }
            }

            return true;
        }

        protected void CountExtraStringsHit(FretBytes chordMask)
        {
            ExtraStringsHit = 0;
            ExtraStringOverstrum = false;

            for (int i = 0; i < 6; i++)
            {
                bool strummedString = ((Strums >> i) & 1) == 1;

                if (chordMask[i] == FretBytes.IGNORE_BYTE && strummedString)
                {
                    ExtraStringsHit++;
                }
            }
        }

        protected override bool CanNoteBeHit(ProGuitarNote note)
        {
            // TODO: Extended sustain stuff?

            return FretBytes.IsFretted(HeldFrets, note.ChordMask);
        }
    }
}