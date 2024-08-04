using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys.Engines
{
    public class YargProKeysEngine : ProKeysEngine
    {
        public YargProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot) : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<ProKeysAction>();

            if (action is ProKeysAction.StarPower)
            {
                // TODO
            }
            else if (action is ProKeysAction.TouchEffects)
            {
                // TODO
            }
            else
            {
                if (gameInput.Button)
                {
                    State.KeyHit = (int) action;
                }
                else
                {
                    State.KeyReleased = (int) action;
                }

                ToggleKey((int) action, gameInput.Button);
                State.KeyPressTimes[(int) action] = gameInput.Time;

                OnKeyStateChange?.Invoke((int) action, gameInput.Button);
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();

            // Update bot (will return if not enabled)
            UpdateBot(time);

            if (State.FatFingerTimer.IsActive)
            {
                // Fat Fingered key was released before the timer expired
                if (State.KeyReleased == State.FatFingerKey && !State.FatFingerTimer.IsExpired(State.CurrentTime))
                {
                    YargLogger.LogFormatDebug("Released fat fingered key at {0}. Note was hit: {1}", State.CurrentTime, State.FatFingerNote!.WasHit);

                    // The note must be hit to disable the timer
                    if (State.FatFingerNote!.WasHit)
                    {
                        YargLogger.LogDebug("Disabling fat finger timer as the note has been hit.");
                        State.FatFingerTimer.Disable();
                        State.FatFingerKey = null;
                        State.FatFingerNote = null;
                    }
                }
                else if(State.FatFingerTimer.IsExpired(State.CurrentTime))
                {
                    YargLogger.LogFormatDebug("Fat Finger timer expired at {0}", State.CurrentTime);

                    var fatFingerKeyMask = 1 << State.FatFingerKey;

                    var isHoldingWrongKey = (State.KeyMask & fatFingerKeyMask) == fatFingerKeyMask;

                    // Overhit if key is still held OR if key is not held but note was not hit either
                    if (isHoldingWrongKey || (!isHoldingWrongKey && !State.FatFingerNote!.WasHit))
                    {
                        YargLogger.LogFormatDebug("Overhit due to fat finger with key {0}. KeyMask: {1}. Holding: {2}. WasHit: {3}",
                            State.FatFingerKey, State.KeyMask, isHoldingWrongKey, State.FatFingerNote!.WasHit);
                        Overhit(State.FatFingerKey!.Value);
                    }

                    State.FatFingerTimer.Disable();
                    State.FatFingerKey = null;
                    State.FatFingerNote = null;
                }
            }

            // Quit early if there are no notes left
            if (State.NoteIndex >= Notes.Count)
            {
                State.KeyHit = null;
                State.KeyReleased = null;
                return;
            }

            CheckForNoteHit();
        }

        protected override void CheckForNoteHit()
        {
            var parentNote = Notes[State.NoteIndex];

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

                bool keysInTime = true;
                foreach (var childNote in parentNote.AllNotes)
                {
                    if (IsKeyInTime(childNote, frontEnd))
                    {
                        YargLogger.LogFormatDebug("Key {0} out of time (Diff: {1})", childNote.Key, childNote.Time - State.KeyPressTimes[childNote.Key]);
                        keysInTime = false;
                        break;
                    }
                }

                // Hit whole note
                if (CanNoteBeHit(parentNote) && keysInTime)
                {
                    YargLogger.LogDebug("Can hit whole note");
                    foreach (var childNote in parentNote.AllNotes)
                    {
                        HitNote(childNote);
                    }

                    State.KeyHit = null;
                }
                else
                {
                    // Note cannot be hit in full, try to use chord staggering logic

                    if (parentNote.IsChord)
                    {
                        // Note is a chord and chord staggering was active and is now expired
                        if (State.ChordStaggerTimer.IsActive && State.ChordStaggerTimer.IsExpired(State.CurrentTime))
                        {
                            YargLogger.LogFormatDebug("Ending chord staggering at {0}", State.CurrentTime);
                            foreach (var note in parentNote.AllNotes)
                            {
                                // This key in the chord was held by the time chord staggering ended, so it can be hit
                                if ((State.KeyMask & note.NoteMask) == note.DisjointMask && IsKeyInTime(note, frontEnd))
                                {
                                    HitNote(note);
                                    YargLogger.LogFormatDebug("Hit staggered note {0} in chord", note.Key);
                                }
                                else
                                {
                                    YargLogger.LogFormatDebug("Missing note {0} due to chord staggering", note.Key);
                                    MissNote(note);
                                }
                            }

                            State.ChordStaggerTimer.Disable();
                        }
                        else
                        {
                            foreach (var note in parentNote.AllNotes)
                            {
                                // Go to next note if the key hit does not match the note's key
                                if (State.KeyHit != note.Key)
                                {
                                    continue;
                                }

                                if (!State.ChordStaggerTimer.IsActive)
                                {
                                    StartTimer(ref State.ChordStaggerTimer, State.CurrentTime);
                                    YargLogger.LogFormatDebug("Starting chord staggering at {0}. End time is {1}",
                                        State.CurrentTime, State.ChordStaggerTimer.EndTime);

                                    var chordStaggerEndTime = State.ChordStaggerTimer.EndTime;

                                    double noteMissTime = note.Time + backEnd;

                                    // Time has surpassed the back end of this note
                                    if (chordStaggerEndTime > noteMissTime)
                                    {
                                        double diff = noteMissTime - chordStaggerEndTime;
                                        StartTimer(ref State.ChordStaggerTimer, State.CurrentTime - Math.Abs(diff));

                                        YargLogger.LogFormatDebug(
                                            "Chord stagger window shortened by {0}. New end time is {1}. Note backend time is {2}",
                                            diff, State.ChordStaggerTimer.EndTime, noteMissTime);
                                    }
                                }

                                State.KeyHit = null;
                                break;
                            }
                        }
                    }
                }
            }

            // If no note was hit but the user hit a key, then over hit
            if (State.KeyHit != null)
            {
                var inWindow = IsNoteInWindow(parentNote);

                ProKeysNote? adjacentNote = null;
                bool isAdjacent = false;
                foreach (var note in parentNote.AllNotes)
                {
                    if (!ProKeysUtilities.IsAdjacentKey(note.Key, State.KeyHit.Value))
                    {
                        continue;
                    }

                    isAdjacent = true;
                    adjacentNote = note;
                    break;
                }

                var isFatFingerActive = State.FatFingerTimer.IsActive;

                if (!inWindow || !isAdjacent || isFatFingerActive)
                {
                    Overhit(State.KeyHit.Value);

                    // TODO Maybe don't disable the timer/use a flag saying no more fat fingers allowed for the current note.

                    State.FatFingerTimer.Disable();
                    State.FatFingerKey = null;
                    State.FatFingerNote = null;
                }
                else
                {
                    StartTimer(ref State.FatFingerTimer, State.CurrentTime);
                    State.FatFingerKey = State.KeyHit.Value;

                    State.FatFingerNote = adjacentNote;

                    YargLogger.LogFormatDebug("Hit adjacent key {0} Starting fat finger timer at {1}. End time: {2}. Key is {3}", State.FatFingerKey, State.CurrentTime,
                        State.FatFingerTimer.EndTime, State.FatFingerKey);
                }

                State.KeyHit = null;
            }
        }

        protected override bool CanNoteBeHit(ProKeysNote note)
        {
            return (State.KeyMask & note.NoteMask) == note.NoteMask;
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

            // // Disables keys that are not in the current note
            // int key = 0;
            // for (var mask = State.KeyHeldMaskVisual; mask > 0; mask >>= 1)
            // {
            //     if ((mask & 1) == 1 && (note.NoteMask & 1 << key) == 0)
            //     {
            //         MutateStateWithInput(new GameInput(note.Time, key, false));
            //     }
            //
            //     key++;
            // }
            //
            // // Press keys for current note
            // foreach (var chordNote in note.AllNotes)
            // {
            //     MutateStateWithInput(new GameInput(note.Time, chordNote.Key, true));
            //     CheckForNoteHit();
            // }
        }
    }
}