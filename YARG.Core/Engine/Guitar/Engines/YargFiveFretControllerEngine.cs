using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretControllerEngine : YargFiveFretEngine
    {
        /// <summary>
        /// For gamepad mode, the amount of time you have between hitting one fret of a chord and the other(s).
        /// Hitting a chord ends it, and if it expires you overstrum.
        /// StrumLeniencyTimer is used for this first, and *then* ChordLeniencyTimer. It makes sense, trust me.
        /// </summary>
        protected EngineTimer GamepadModeChordLeniencyTimer;

        /// <summary>
        /// This mask stores the frets that should *not* trigger a strum the next time they are
        /// released/lifted, because they were being used to hold a sustain, and releasing
        /// after a sustain should not trigger a strum.
        /// </summary>
        private int PressedSustainsMask;

        private bool _noteJustHitInTheMiddleOfUpdateHitLogic;

        public YargFiveFretControllerEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
            GamepadModeChordLeniencyTimer = new EngineTimer(engineParameters.GamepadModeChordLeniency);
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            base.MutateStateWithInput(gameInput);
            HasStrummed = false;

            if (IsFretPress) HasStrummed = true;
            else if (!IsFretPress && EngineParameters.GamepadModeStrumOnRelease) {
                HasStrummed = true;
                    
                // We don't want to strum on release if we're releasing a fret that's part of an active sustain
                // TODO: Get rid of this. Bye bye now. Goodbye.
                var droppedMask = LastButtonMask & ~ButtonMask;
                if ((droppedMask & PressedSustainsMask) != 0) {
                    HasStrummed = false;
                    PressedSustainsMask &= ~droppedMask;
                }
            }
        }

        protected override void UpdateHitLogic(double time) {
            if (HasStrummed && !HopoLeniencyTimer.IsActive && StrumLeniencyTimer.IsActive) 
            {                // Chord leniency is handled here.
                StartTimer(ref GamepadModeChordLeniencyTimer, CurrentTime);

                // Update hit logic but with this timer disabled so that we don't overstrum.
                double startTime = StrumLeniencyTimer.StartTime;
                StrumLeniencyTimer.Disable();

                base.UpdateHitLogic(time);

                // Afterwards, restore the timer to how it was.
                if (!_noteJustHitInTheMiddleOfUpdateHitLogic) StrumLeniencyTimer.Start(startTime);
                else _noteJustHitInTheMiddleOfUpdateHitLogic = false;
            }
            else
            {
                bool wasTimerActive = StrumLeniencyTimer.IsActive;
                double startTime = StrumLeniencyTimer.StartTime;
                bool previousHasFretted = HasFretted;
                bool previousIsFretPress = IsFretPress;
                base.UpdateHitLogic(time);
                // Don't overstrum if we released and not pressed
                if (previousHasFretted && !previousIsFretPress && StrumLeniencyTimer.IsActive)
                {
                    if (wasTimerActive) StrumLeniencyTimer.Start(startTime);
                    else StrumLeniencyTimer.Disable();
                };
            }
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            if (GamepadModeChordLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(GamepadModeChordLeniencyTimer.EndTime, CurrentTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing gamepad mode chord leniency end time at {0}",
                        GamepadModeChordLeniencyTimer.EndTime);
                    QueueUpdateTime(GamepadModeChordLeniencyTimer.EndTime, "Gamepad Mode Chord Leniency End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            base.Reset(keepCurrentButtons);
            GamepadModeChordLeniencyTimer.Disable();
        }

        public override void SetSpeed(double speed)
        {
            base.SetSpeed(speed);
            GamepadModeChordLeniencyTimer.SetSpeed(speed);
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            // In gamepad mode, on a release, we use LastButtonMask instead of ButtonMask.
            // This is because, if you're *releasing*, then the fret that the note you want to hit is on *isn't actually being held*, because, well, you released it.
            // But you should still be able to hit it -- that's the whole point.
            byte stored = ButtonMask;
            if (HasFretted && !IsFretPress) ButtonMask = LastButtonMask;
            bool result = base.CanNoteBeHit(note);

            ButtonMask = stored;
            return result;
        }

        protected override void HitNote(GuitarNote note)
        {
            base.HitNote(note);
            YargLogger.LogDebug($"Is it a chord? The answer is ...... {note.IsChord}! Dun dun dun");
            if (note.IsChord) 
            {
                GamepadModeChordLeniencyTimer.Disable();
            }
            if (note.IsSustain) PressedSustainsMask |= note.IsDisjoint ? note.DisjointMask : note.NoteMask;

            _noteJustHitInTheMiddleOfUpdateHitLogic = true;
        }

        protected override void UpdateTimers()
        {
            if (StrumLeniencyTimer.IsActive && StrumLeniencyTimer.IsExpired(CurrentTime)) {
                // We intentionally DON'T do this:
                //if (GamepadModeChordLeniencyTimer.IsActive) Overstrum();
                // It might result in some overstrums not being punished, 
                // but those overstrums would need to be very... unnatural overstrums. I think it's okay.

                GamepadModeChordLeniencyTimer.Start(CurrentTime);
                StrumLeniencyTimer.Disable();
                YargLogger.LogDebug("We starting bruhhh");
            }

            if (GamepadModeChordLeniencyTimer.IsActive && GamepadModeChordLeniencyTimer.IsExpired(CurrentTime)) {
                Overstrum();
                GamepadModeChordLeniencyTimer.Disable();
                ReRunHitLogic = true; // For whoever reviews this PR: Why is this here in the above (strum leniency thing)? Do I need to do it here? Not sure. I did it just in case. Anyways either way I need to remove this comment later.
            }

            base.UpdateTimers();
        }
    }
}