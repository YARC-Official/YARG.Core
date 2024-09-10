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
                }
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();
            // UpdateTimers();

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

            Strums = 0;
            HasFretted = false;
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
                    YargLogger.LogFormatTrace("Hit note (Index: {0}) at {1} with hopo rules",
                        i, CurrentTime);
                    break;
                }

                // If hopo/tap checks failed then the note can be hit if it was strummed
                if (Strums != 0 &&
                    (isFirstNoteInWindow || (NoteIndex > 0 && EngineStats.Combo == 0)))
                {
                    HitNote(note);
                    if (Strums != 0)
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}) at {1} with strum input",
                            i, CurrentTime);
                    }
                    else
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}) at {1} with strum leniency",
                            i, CurrentTime);
                    }

                    break;
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