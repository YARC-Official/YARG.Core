using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Drums.Engines
{
    public class YargDrumsEngine : DrumsEngine
    {
        public YargDrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack,
            DrumsEngineParameters engineParameters, bool isBot, bool isMidiDrumsInput)
            : base(chart, syncTrack, engineParameters, isBot, isMidiDrumsInput)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            // Do not use gameInput.Button here!
            // Drum inputs are handled as axes, not buttons, for velocity support.
            // Every button release has its gameInput.Axis set to 0, so this works safely.
            if (gameInput.Axis > 0)
            {
                if (IsMidiDrumsInput)
                {
                    var eliteDrumsAction = gameInput.GetAction<EliteDrumsAction>();
                    Action = ConvertMidiDrumsInput(eliteDrumsAction, Chart.Instrument);
                    PadHit = Action is null ? null : ConvertInputToPad(EngineParameters.Mode, Action.Value);
                }
                else
                {
                    Action = gameInput.GetAction<DrumsAction>();
                    PadHit = ConvertInputToPad(EngineParameters.Mode, gameInput.GetAction<DrumsAction>());
                }
                HitVelocity = gameInput.Axis;

                if (PadHit != null)
                {
                    SubmitLaneNote((int) PadHit);
                }
            }
        }

        private DrumsAction? ConvertMidiDrumsInput(EliteDrumsAction action, Instrument target)
        {
            if (target is Instrument.FourLaneDrums or Instrument.ProDrums)
            {
                return action switch
                {
                    EliteDrumsAction.Kick => DrumsAction.Kick,
                    EliteDrumsAction.FourLaneRedDrum => DrumsAction.Drum1,
                    EliteDrumsAction.FourLaneYellowDrum => DrumsAction.Drum2,
                    EliteDrumsAction.FourLaneBlueDrum => DrumsAction.Drum3,
                    EliteDrumsAction.FourLaneGreenDrum => DrumsAction.Drum4,
                    EliteDrumsAction.FourLaneYellowCymbal => DrumsAction.Cymbal1,
                    EliteDrumsAction.FourLaneBlueCymbal => DrumsAction.Cymbal2,
                    EliteDrumsAction.FourLaneGreenCymbal => DrumsAction.Cymbal3,
                    _ => null
                };
            }

            if (target is Instrument.FiveLaneDrums)
            {
                return action switch
                {
                    EliteDrumsAction.Kick => DrumsAction.Kick,
                    EliteDrumsAction.FiveLaneRedDrum => DrumsAction.Drum1,
                    EliteDrumsAction.FiveLaneYellowCymbal => DrumsAction.Cymbal1,
                    EliteDrumsAction.FiveLaneBlueDrum => DrumsAction.Drum3,
                    EliteDrumsAction.FiveLaneOrangeCymbal => DrumsAction.Cymbal2,
                    EliteDrumsAction.FiveLaneGreenDrum => DrumsAction.Drum4,
                    _ => null
                };
            }

            return null;
        }

        protected override void UpdateHitLogic(double time)
        {
            // Update bot (will return if not enabled)
            UpdateBot(time);

            // Only check hit if there are notes left
            if (NoteIndex < Notes.Count)
            {
                CheckForNoteHit();
            }
            else if (Action is { } padAction)
            {
                OnPadHit?.Invoke(padAction, false, false, DrumNoteType.Neutral, HitVelocity.GetValueOrDefault(0));
                ResetPadState();
            }
        }

        protected override void CheckForNoteHit()
        {
            for (int i = NoteIndex; i < Notes.Count; i++)
            {
                bool isFirstNoteInWindow = i == NoteIndex;
                bool stopSkipping = false;

                var parentNote = Notes[i];

                // For drums, each note in the chord are treated separately
                foreach (var note in parentNote.AllNotes)
                {
                    // Miss out the back end
                    if (!IsNoteInWindow(note, out bool missed))
                    {
                        // You can't skip ahead if the note is not in the hit window to begin with
                        stopSkipping = true;

                        if (isFirstNoteInWindow && missed)
                        {
                            // If one of the notes in the chord was missed out the back end,
                            // that means all of them would miss.
                            foreach (var missedNote in parentNote.AllNotes)
                            {
                                // Intercept missed note while lane phrase is active and missed note is in forgiveness window
                                if (HitNoteFromLane(missedNote))
                                {
                                    continue;
                                }

                                // Allow drummers to skip SP activation notes without being penalized.
                                if (missedNote.IsStarPowerActivator && CanStarPowerActivate)
                                {
                                    HitNote(missedNote, true);
                                    continue;
                                }
                                MissNote(missedNote);
                            }
                        }

                        break;
                    }

                    // Hit note
                    if (CanNoteBeHit(note))
                    {
                        bool awardVelocityBonus = ApplyVelocity(note);

                        // TODO - Deadly Dynamics modifier check on awardVelocityBonus

                        HitNote(note);
                        OnPadHit?.Invoke(Action!.Value, true, awardVelocityBonus, note.Type, HitVelocity.GetValueOrDefault(0));

                        if (awardVelocityBonus)
                        {
                            const int velocityBonus = POINTS_PER_NOTE / 2;
                            AddScore(velocityBonus);
                            EngineStats.DynamicsBonus += velocityBonus;
                            YargLogger.LogFormatTrace("Velocity bonus of {0} points was awarded to a note at tick {1}.", velocityBonus, note.Tick);

                            if(note.IsAccent)
                            {
                                EngineStats.AccentsHit++;
                            }
                            else if(note.IsGhost)
                            {
                                EngineStats.GhostsHit++;
                            }
                        }

                        ResetPadState();

                        // You can't hit more than one note with the same input
                        stopSkipping = true;
                        break;
                    }
                    else
                    {
                        //YargLogger.LogFormatDebug("Cant hit note (Index: {0}) at {1}.", i, CurrentTime);
                    }
                }

                if (stopSkipping)
                {
                    break;
                }
            }

            // If no note was hit but the user hit a pad, then over hit
            if (PadHit != null)
            {
                OnPadHit?.Invoke(Action!.Value, false, false, DrumNoteType.Neutral, HitVelocity.GetValueOrDefault(0));
                Overhit();
                ResetPadState();
            }
        }

        protected override bool CanNoteBeHit(DrumNote note)
        {
            return note.Pad == PadHit;
        }

        protected override void UpdateBot(double time)
        {
            if (!IsBot || NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[NoteIndex];

            if (time < note.Time)
            {
                return;
            }

            // Each note in the "chord" is hit separately on drums
            foreach (var chordNote in note.AllNotes)
            {
                Action = ConvertPadToAction(EngineParameters.Mode, chordNote.Pad);
                PadHit = chordNote.Pad;
                CheckForNoteHit();
            }
        }

        private void ResetPadState()
        {
            Action = null;
            PadHit = null;
            HitVelocity = null;
        }
    }
}