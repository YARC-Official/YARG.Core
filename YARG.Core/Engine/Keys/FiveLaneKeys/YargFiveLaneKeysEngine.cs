using System;
using System.Runtime.CompilerServices;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Engine.Keys;

namespace YARG.Core.Engine.Keys.Engines
{
    public class YargFiveLaneKeysEngine : FiveLaneKeysEngine
    {
        private KeyPressedTimes[] _keyPressedTimes = new KeyPressedTimes[7];

        public YargFiveLaneKeysEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot) : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            // These should always be null before inputs are processed
            YargLogger.Assert(KeyHitThisUpdate == null, "KeyHitThisUpdate was not handled!");
            YargLogger.Assert(KeyReleasedThisUpdate == null, "KeyReleasedThisUpdate was not handled!");

            var action = gameInput.GetAction<ProKeysAction>();

            if (!IsFiveLaneKeysAction(action))
            {
                return;
            }
            if (action is ProKeysAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
            else if (action is ProKeysAction.TouchEffects)
            {
                StartWhammyTimer(gameInput.Time);
            }
            else
            {
                var fiveLaneKeyIndex = (int)ProKeysActionToFiveLaneKeysAction(action);

                if (gameInput.Button)
                {
                    KeyHitThisUpdate = fiveLaneKeyIndex;
                    _keyPressedTimes[fiveLaneKeyIndex].NoteIndex = NoteIndex;
                    _keyPressedTimes[fiveLaneKeyIndex].Time = gameInput.Time;
                }
                else
                {
                    KeyReleasedThisUpdate = fiveLaneKeyIndex;
                }

                PreviousKeyMask = KeyMask;
                ToggleKey(fiveLaneKeyIndex, gameInput.Button);
                KeyPressTimes[fiveLaneKeyIndex] = gameInput.Time;

                OnKeyStateChange?.Invoke(fiveLaneKeyIndex, gameInput.Button);
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            // Update bot (will return if not enabled)
            UpdateBot(time);

            // Only check note logic if note index is within bounds
            if (NoteIndex < Notes.Count)
            {
                CheckForNoteHit();
            }

            KeyHitThisUpdate = null;
            KeyReleasedThisUpdate = null;
            UpdateSustains();
        }

        protected override void CheckForNoteHit()
        {
            var parentNote = Notes[NoteIndex];

            // Miss out the back end
            if (!IsNoteInWindow(parentNote, out bool missed))
            {
                if (missed)
                {
                    // If one of the notes in the chord was missed out the back end,
                    // that means all of them would miss.
                    foreach (var missedNote in parentNote.AllNotes)
                    {
                        MissNote(missedNote);
                    }
                }
            }
            else
            {
                double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(parentNote));
                double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);
                double backEnd = EngineParameters.HitWindow.GetBackEnd(hitWindow);

                // Hit whole note
                if (CanNoteBeHit(parentNote))
                {
                    foreach (var childNote in parentNote.AllNotes)
                    {
                        HitNote(childNote);
                    }

                    KeyHitThisUpdate = null;
                }
                else
                {
                    // Note cannot be hit in full, try to use chord staggering logic

                    if (parentNote.IsChord)
                    {
                        // Note is a chord and chord staggering was active and is now expired
                        if (ChordStaggerTimer.IsActive && ChordStaggerTimer.IsExpired(CurrentTime))
                        {
                            YargLogger.LogFormatTrace("Ending chord staggering at {0}", CurrentTime);
                            foreach (var note in parentNote.AllNotes)
                            {
                                // This key in the chord was held by the time chord staggering ended, so it can be hit
                                if ((KeyMask & note.DisjointMask) == note.DisjointMask && IsKeyInTime(note, frontEnd))
                                {
                                    HitNote(note);
                                    YargLogger.LogFormatTrace("Hit staggered note {0} in chord", (int)note.FiveLaneKeysAction);
                                }
                                else
                                {
                                    YargLogger.LogFormatTrace("Missing note {0} due to chord staggering", (int)note.FiveLaneKeysAction);
                                    MissNote(note);
                                }
                            }

                            ChordStaggerTimer.Disable(CurrentTime);
                        }
                        else
                        {
                            foreach (var note in parentNote.AllNotes)
                            {
                                // Go to next note if the key hit does not match the note's key
                                if (KeyHitThisUpdate != (int)note.FiveLaneKeysAction)
                                {
                                    continue;
                                }

                                if (!ChordStaggerTimer.IsActive)
                                {
                                    StartTimer(ref ChordStaggerTimer, CurrentTime);
                                    YargLogger.LogFormatTrace("Starting chord staggering at {0}. End time is {1}",
                                        CurrentTime, ChordStaggerTimer.EndTime);

                                    var chordStaggerEndTime = ChordStaggerTimer.EndTime;

                                    double noteMissTime = note.Time + backEnd;

                                    // Time has surpassed the back end of this note
                                    if (chordStaggerEndTime > noteMissTime)
                                    {
                                        double diff = noteMissTime - chordStaggerEndTime;
                                        StartTimer(ref ChordStaggerTimer, CurrentTime - Math.Abs(diff));

                                        YargLogger.LogFormatTrace(
                                            "Chord stagger window shortened by {0}. New end time is {1}. Note backend time is {2}",
                                            diff, ChordStaggerTimer.EndTime, noteMissTime);
                                    }
                                }

                                KeyHitThisUpdate = null;
                                break;
                            }
                        }
                    }
                }
            }

            // If no note was hit but the user hit a key, then over hit
            if (KeyHitThisUpdate != null)
            {
                Overhit(KeyHitThisUpdate.Value);
                KeyHitThisUpdate = null;
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

            if ((KeyMask & note.NoteMask) == note.NoteMask)
            {
                foreach (var childNote in note.AllNotes)
                {
                    if (!IsKeyInTime(childNote, frontEnd))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        protected override void UpdateBot(double time)
        {
            float botNoteHoldTime = 0.166f;
            GuitarNote? note = null;
            int keysInSustain = 0;

            if (!IsBot)
            {
                return;
            }

            IsStarPowerInputActive = CanStarPowerActivate && !IsStarPowerInputActive;

            if (NoteIndex < Notes.Count)
            {
                note = Notes[NoteIndex];
            }

            // Find the active sustains
            foreach (var sustain in ActiveSustains)
            {
                keysInSustain |= 1 << (int)sustain.Note.FiveLaneKeysAction;
            }

            // Release no longer needed keys
            int key = 0;
            for (var mask = KeyMask; mask > 0; mask >>= 1)
            {
                // Keys are not released if they are part of an active sustain
                // or were pressed less than botNoteHoldTime in the past,
                // unless the key is going to be pressed again this update
                // or it is the next key to be pressed and half of the time
                // between press and next press has already elapsed or another
                // key is being pressed this update
                bool keyProtected = false;
                bool currentKey;

                if (note is not null)
                {
                    currentKey = time >= note.Time && (int)note.FiveLaneKeysAction == key;
                }
                else
                {
                    currentKey = false;
                }

                if (!currentKey)
                {
                    if ((keysInSustain & 1 << key) != 0)
                    {
                        keyProtected = true;
                    }
                    else if (_keyPressedTimes[key].Time > time - botNoteHoldTime)
                    {
                        keyProtected = true;

                        // Release the key if the next note is on this key and
                        // half of the time between press and the next note has elapsed
                        if (note is not null)
                        {
                            // Despite the name chordNote, this also applies to single notes
                            foreach (var chordNote in note.AllNotes)
                            {
                                if ((int)chordNote.FiveLaneKeysAction == key && chordNote.Time - time < time - _keyPressedTimes[key].Time)
                                {
                                    keyProtected = false;
                                    break;
                                }
                            }
                        }
                    }

                    // if the key isn't protected due to a sustain and another note is being played, release the key
                    if (note is not null && (keysInSustain & 1 << key) == 0 && time >= note.Time)
                    {
                        keyProtected = false;
                    }
                }

                if ((mask & 1) == 1 && (!keyProtected || currentKey))
                {
                    var pressedNote = Notes[_keyPressedTimes[key].NoteIndex];

                    // We loop to ensure that all notes in a chord are released at the same time
                    foreach (var chordNote in pressedNote.AllNotes)
                    {
                        if ((keysInSustain & 1 << (int)chordNote.FiveLaneKeysAction) == 0)
                        {
                            var action = FiveLaneKeysActionToProKeysAction(chordNote.FiveLaneKeysAction);
                            MutateStateWithInput(new GameInput(time, (int)action, false));
                            // Nothing else is going to reset this for a bot, so we have to do it
                            KeyReleasedThisUpdate = null;
                        }
                    }
                }

                key++;
            }


            if (NoteIndex >= Notes.Count)
            {
                // Nothing left to press
                return;
            }

            if (time < note!.Time)
            {
                // It isn't time to press another key yet
                return;
            }

            // Press keys for current note
            foreach (var chordNote in note.AllNotes)
            {
                // Need to translate back to an actual Pro Keys Action for the GameInput
                var action = FiveLaneKeysActionToProKeysAction(chordNote.FiveLaneKeysAction);

                MutateStateWithInput(new GameInput(note.Time, (int)action, true));
                CheckForNoteHit();
            }
        }

        private static ProKeysAction FiveLaneKeysActionToProKeysAction(FiveLaneKeysAction fiveLaneKeysAction)
        {
            return fiveLaneKeysAction switch
            {
                FiveLaneKeysAction.OpenNote => ProKeysAction.OpenNote,
                FiveLaneKeysAction.GreenKey => ProKeysAction.GreenKey,
                FiveLaneKeysAction.RedKey => ProKeysAction.RedKey,
                FiveLaneKeysAction.YellowKey => ProKeysAction.YellowKey,
                FiveLaneKeysAction.BlueKey => ProKeysAction.BlueKey,
                FiveLaneKeysAction.OrangeKey => ProKeysAction.OrangeKey,
                _ => throw new Exception("Unhandled.")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiveLaneKeysAction(ProKeysAction action)
        {
            return (ALLOWED_FIVE_LANE_KEYS_ACTIONS & (1 << (int) action)) != 0;
        }

        private const int ALLOWED_FIVE_LANE_KEYS_ACTIONS =
            1 << (int) ProKeysAction.GreenKey |
            1 << (int) ProKeysAction.RedKey |
            1 << (int) ProKeysAction.YellowKey |
            1 << (int) ProKeysAction.BlueKey |
            1 << (int) ProKeysAction.OrangeKey |
            1 << (int) ProKeysAction.OpenNote |
            1 << (int) ProKeysAction.StarPower |
            1 << (int) ProKeysAction.TouchEffects;
    }
}
