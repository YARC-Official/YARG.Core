using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Keys.Engines
{
    public class YargProKeysEngine : ProKeysEngine
    {
        private KeyPressedTimes[] _keyPressedTimes = new KeyPressedTimes[(int)ProKeysAction.Key25 + 1];

        public YargProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot) : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            // These should always be null before inputs are processed
            YargLogger.Assert(KeyHitThisUpdate == null, "KeyHitThisUpdate was not handled!");
            YargLogger.Assert(KeyReleasedThisUpdate == null, "KeyReleasedThisUpdate was not handled!");

            var action = gameInput.GetAction<ProKeysAction>();

            if (action is ProKeysAction.OpenNote or ProKeysAction.GreenKey or ProKeysAction.RedKey
                or ProKeysAction.YellowKey or ProKeysAction.BlueKey or ProKeysAction.OrangeKey)
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
                if (gameInput.Button)
                {
                    KeyHitThisUpdate = (int) action;
                    _keyPressedTimes[(int) action].NoteIndex = NoteIndex;
                    _keyPressedTimes[(int) action].Time = gameInput.Time;
                    SubmitLaneNote((int) action);
                }
                else
                {
                    KeyReleasedThisUpdate = (int) action;
                }

                PreviousKeyMask = KeyMask;
                ToggleKey((int) action, gameInput.Button);
                KeyPressTimes[(int) action] = gameInput.Time;

                OnKeyStateChange?.Invoke((int) action, gameInput.Button);
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            // Update bot (will return if not enabled)
            UpdateBot(time);

            if (FatFingerTimer.IsActive)
            {
                // Fat Fingered key was released before the timer expired
                if (KeyReleasedThisUpdate == FatFingerKey && !FatFingerTimer.IsExpired(CurrentTime))
                {
                    YargLogger.LogFormatTrace("Released fat fingered key at {0}. Note was hit: {1}", CurrentTime, FatFingerNote!.WasHit);

                    // The note must be hit to disable the timer
                    if (FatFingerNote!.WasHit)
                    {
                        YargLogger.LogTrace("Disabling fat finger timer as the note has been hit. Fat Finger was Ignored.");
                        FatFingerTimer.Disable(time, early: true);
                        FatFingerKey = null;
                        FatFingerNote = null;

                        EngineStats.FatFingersIgnored++;
                    }
                }
                else if(FatFingerTimer.IsExpired(CurrentTime))
                {
                    YargLogger.LogFormatTrace("Fat Finger timer expired at {0}", CurrentTime);

                    var fatFingerKeyMask = 1 << FatFingerKey;

                    var isHoldingWrongKey = (KeyMask & fatFingerKeyMask) == fatFingerKeyMask;

                    // Overhit if key is still held OR note was not hit
                    if (isHoldingWrongKey || !FatFingerNote!.WasHit)
                    {
                        YargLogger.LogFormatTrace("Overhit due to fat finger with key {0}. KeyMask: {1}. Holding: {2}. WasHit: {3}",
                            FatFingerKey, KeyMask, isHoldingWrongKey, FatFingerNote!.WasHit);
                        Overhit(FatFingerKey!.Value);
                    }
                    else
                    {
                        EngineStats.FatFingersIgnored++;
                        YargLogger.LogFormatTrace("Fat finger was ignored. KeyMask: {0}. Holding: {1}. WasHit: {2}",
                            KeyMask, isHoldingWrongKey, FatFingerNote!.WasHit);
                    }

                    FatFingerTimer.Disable(time);
                    FatFingerKey = null;
                    FatFingerNote = null;
                }
            }

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
                    // Intercept missed note while lane phrase is active
                    if (!HitNoteFromLane(parentNote))
                    {
                        // If one of the notes in the chord was missed out the back end,
                        // that means all of them would miss.
                        foreach (var missedNote in parentNote.AllNotes)
                        {
                            MissNote(missedNote);
                        }
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
                                    YargLogger.LogFormatTrace("Hit staggered note {0} in chord", note.Key);
                                }
                                else
                                {
                                    YargLogger.LogFormatTrace("Missing note {0} due to chord staggering", note.Key);
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
                                if (KeyHitThisUpdate != note.Key)
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
                static ProKeysNote? CheckForAdjacency(ProKeysNote fullNote, int key)
                {
                    foreach (var note in fullNote.AllNotes)
                    {
                        if (ProKeysUtilities.IsAdjacentKey(note.Key, key))
                        {
                            return note;
                        }
                    }

                    return null;
                }

                ProKeysNote? adjacentNote;
                bool isAdjacent;
                bool inWindow;

                // Try to fat finger previous note first

                // Previous note can only be fat fingered if the current distance from the note
                // is within the fat finger threshold (default 100ms)
                if (parentNote.PreviousNote is not null
                    && CurrentTime - parentNote.PreviousNote.Time < FatFingerTimer.SpeedAdjustedThreshold)
                {
                    adjacentNote = CheckForAdjacency(parentNote.PreviousNote, KeyHitThisUpdate.Value);
                    isAdjacent = adjacentNote != null;
                    inWindow = IsNoteInWindow(parentNote.PreviousNote, out _);

                }
                // Try to fat finger current note (upcoming note)
                else
                {
                    adjacentNote = CheckForAdjacency(parentNote, KeyHitThisUpdate.Value);
                    isAdjacent = adjacentNote != null;
                    inWindow = IsNoteInWindow(parentNote, out _);
                }

                var isFatFingerActive = FatFingerTimer.IsActive;

                if (!inWindow || !isAdjacent || isFatFingerActive)
                {
                    Overhit(KeyHitThisUpdate.Value);

                    // TODO Maybe don't disable the timer/use a flag saying no more fat fingers allowed for the current note.

                    FatFingerTimer.Disable(CurrentTime);
                    FatFingerKey = null;
                    FatFingerNote = null;
                }
                else
                {
                    StartTimer(ref FatFingerTimer, CurrentTime);
                    FatFingerKey = KeyHitThisUpdate.Value;

                    FatFingerNote = adjacentNote;

                    YargLogger.LogFormatTrace("Hit adjacent key {0} to note {1}. Starting fat finger timer at {2}. End time: {3}. Key is {4}", FatFingerKey, adjacentNote!.Key, CurrentTime,
                        FatFingerTimer.EndTime, FatFingerKey);
                }

                KeyHitThisUpdate = null;
            }
        }

        protected override bool CanNoteBeHit(ProKeysNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

            if((KeyMask & note.NoteMask) == note.NoteMask)
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

            // Glissando hit logic
            // Forces the first glissando to be hit correctly, then the rest can be hit "loosely"
            if (note.PreviousNote is not null && note.IsGlissando && note.PreviousNote.IsGlissando)
            {
                var keyDiff = KeyMask ^ PreviousKeyMask;
                var keysPressed = keyDiff & KeyMask;
                //var keysReleased = keyDiff & PreviousKeyMask;

                foreach (var child in note.AllNotes)
                {
                    var pressCopy = keysPressed;

                    int i = 0;
                    while (pressCopy > 0)
                    {
                        if((pressCopy & 1) != 0 && IsKeyInTime(child, i, frontEnd))
                        {
                            // It's not ideal that this is here but there's no way to know what key hit the note
                            // within HitNote() so we have to set the press time here
                            KeyPressTimes[i] = DEFAULT_PRESS_TIME;
                            return true;
                        }

                        i++;
                        pressCopy >>= 1;
                    }
                }
            }

            return false;
        }

        protected override void UpdateBot(double time)
        {
            float botNoteHoldTime = 0.166f;
            ProKeysNote? note = null;
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
                keysInSustain |= 1 << sustain.Note.Key;
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
                    currentKey = time >= note.Time && note.Key == key;
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
                                if (chordNote.Key == key && chordNote.Time - time < time - _keyPressedTimes[key].Time)
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
                        if ((keysInSustain & 1 << chordNote.Key) == 0)
                        {
                            MutateStateWithInput(new GameInput(time, chordNote.Key, false));
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
                MutateStateWithInput(new GameInput(note.Time, chordNote.Key, true));
                CheckForNoteHit();
            }
        }
    }
}
