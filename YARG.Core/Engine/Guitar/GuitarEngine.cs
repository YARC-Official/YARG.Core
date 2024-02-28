using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarEngineParameters,
        GuitarStats, GuitarEngineState>
    {
        protected sealed class ActiveSustain
        {
            public GuitarNote Note;
            public uint BaseTick;
            public double BaseScore;

            public bool HasFinishedScoring;

            public ActiveSustain(GuitarNote note)
            {
                Note = note;
                BaseTick = note.Tick;
            }

            public double GetEndTime(SyncTrack syncTrack, uint sustainBurstThreshold)
            {
                return syncTrack.TickToTime(Note.TickEnd - sustainBurstThreshold);
            }
        }

        public delegate void OverstrumEvent();

        public delegate void SustainStartEvent(GuitarNote note);

        public delegate void SustainEndEvent(GuitarNote note, double timeEnded, bool finished);

        public OverstrumEvent?    OnOverstrum;
        public SustainStartEvent? OnSustainStart;
        public SustainEndEvent?   OnSustainEnd;

        protected List<ActiveSustain> ActiveSustains = new();

        protected GuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters, false)
        {
            State.Initialize(engineParameters);
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            byte buttons = State.FretMask;
            ActiveSustains.Clear();

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                State.FretMask = buttons;
            }
        }

        protected virtual void Overstrum()
        {
            // Can't overstrum before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (State.NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                i--;

                double finalScore = CalculateSustainPoints(sustain, State.CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, State.CurrentTime, sustain.HasFinishedScoring);
            }

            if (State.NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[State.NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[State.NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overstrums++;

            UpdateMultiplier();

            OnOverstrum?.Invoke();
        }

        protected override void HitNote(GuitarNote note)
        {
            note.SetHitState(true, true);

            // Detect if the last note(s) were skipped
            bool skipped = false;
            var prevNote = note.PreviousNote;
            while (prevNote is not null && !prevNote.WasHit && !prevNote.WasMissed)
            {
                skipped = true;
                MissNote(prevNote);

                EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
                {
                    NoteTime = prevNote.Time,
                    NoteLength = prevNote.TimeLength,
                    NoteIndex = State.NoteIndex,
                    NoteMask = prevNote.NoteMask,
                    WasHit = false,
                    WasSkipped = true,
                });

                prevNote = prevNote.PreviousNote;
            }

            if (note.IsStarPower && note.IsStarPowerEnd)
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (State.IsSoloActive)
            {
                Solos[State.CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }

            EngineStats.Combo++;

            if (EngineStats.Combo > EngineStats.MaxCombo)
            {
                EngineStats.MaxCombo = EngineStats.Combo;
            }

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            if (note.IsDisjoint)
            {
                foreach (var chordNote in note.ChordEnumerator())
                {
                    if (!chordNote.IsSustain)
                    {
                        continue;
                    }

                    StartSustain(chordNote);
                }
            }
            else if (note.IsSustain)
            {
                StartSustain(note);
            }

            State.WasNoteGhosted = false;

            EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
            {
                NoteTime = note.Time,
                NoteLength = note.TimeLength,
                NoteIndex = State.NoteIndex,
                NoteMask = note.NoteMask,
                WasHit = true,
                WasSkipped = skipped,
            });

            OnNoteHit?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
        }

        protected override void MissNote(GuitarNote note)
        {
            note.SetMissState(true, true);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }
            if (note.IsSoloStart)
            {
                StartSolo();
            }

            State.WasNoteGhosted = false;

            EngineStats.Combo = 0;

            UpdateMultiplier();

            EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
            {
                NoteTime = note.Time,
                NoteLength = note.TimeLength,
                NoteIndex = State.NoteIndex,
                NoteMask = note.NoteMask,
                WasHit = false,
                WasSkipped = false,
            });

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
        }

        protected override void AddScore(GuitarNote note)
        {
            int notePoints = POINTS_PER_NOTE * (1 + note.ChildNotes.Count) * EngineStats.ScoreMultiplier;
            AddScore(notePoints);
        }

        protected override void UpdateMultiplier()
        {
            int previousMultiplier = EngineStats.ScoreMultiplier;
            base.UpdateMultiplier();
            int newMultiplier = EngineStats.ScoreMultiplier;

            // Rebase sustains when the multiplier changes so that
            // there aren't huge jumps in points on extended sustains
            if (newMultiplier != previousMultiplier)
            {
                // Temporarily reset multiplier to calculate score correctly
                EngineStats.ScoreMultiplier = previousMultiplier;
                RebaseSustains(State.CurrentTick);
                EngineStats.ScoreMultiplier = newMultiplier;
            }
        }

        protected override void UpdateProgressValues(uint tick)
        {
            base.UpdateProgressValues(tick);

            EngineStats.PendingScore = 0;
            foreach (var sustain in ActiveSustains)
            {
                EngineStats.PendingScore += (int) CalculateSustainPoints(sustain, tick);
            }
        }

        protected override void RebaseProgressValues(uint baseTick)
        {
            base.RebaseProgressValues(baseTick);
            RebaseStarPowerWhammy(baseTick);
            RebaseSustains(baseTick);
        }

        protected void RebaseStarPowerWhammy(uint baseTick)
        {
            if (baseTick < State.StarPowerWhammyBaseTick)
                YargTrace.Fail($"Star Power whammy base tick cannot go backwards! Went from {State.StarPowerWhammyBaseTick} to {baseTick}");

            State.StarPowerWhammyBaseTick = baseTick;
        }

        protected void RebaseSustains(uint baseTick)
        {
            EngineStats.PendingScore = 0;
            foreach (var sustain in ActiveSustains)
            {
                // Don't rebase sustains that haven't started yet
                if (baseTick < sustain.BaseTick)
                {
                    // Only fail when the sustain has actually started
                    if (baseTick >= sustain.Note.Tick)
                        YargTrace.Fail($"Sustain base tick cannot go backwards! Attempted to go from {sustain.BaseTick} to {baseTick}");

                    continue;
                }

                double sustainScore = CalculateSustainPoints(sustain, baseTick);

                sustain.BaseTick = Math.Clamp(baseTick, sustain.Note.Tick, sustain.Note.TickEnd);
                sustain.BaseScore = sustainScore;
                EngineStats.PendingScore += (int) sustainScore;
            }
        }

        protected override double CalculateStarPowerGain(uint tick)
        {
            if (State.StarPowerWhammyTimer.IsExpired(State.CurrentTime))
            {
                // We need to clamp the tick value to the max possible time of the threshold,
                // otherwise the whammy gain will incorrectly reset to 0 momentarily
                double endTime = State.StarPowerWhammyTimer.EndTime;
                tick = SyncTrack.TimeToTick(endTime);
            }
            else if (!State.StarPowerWhammyTimer.IsEnabled)
            {
                return 0;
            }

            return CalculateStarPowerBeatProgress(tick, State.StarPowerWhammyBaseTick);
        }

        protected void StartSustain(GuitarNote note)
        {
            var sustain = new ActiveSustain(note);

            ActiveSustains.Add(sustain);
            AddConsistencyAnchor(sustain.GetEndTime(SyncTrack, SustainBurstThreshold));

            OnSustainStart?.Invoke(note);
        }

        protected void UpdateSustains()
        {
            EngineStats.PendingScore = 0;

            bool isStarPowerSustainActive = false;
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                var note = sustain.Note;

                isStarPowerSustainActive |= note.IsStarPower;

                // If we're close enough to the end of the sustain, finish it
                // Provides leniency for sustains with no gap (and just in general)
                bool isBurst = (int) (note.TickEnd - State.CurrentTick) <= SustainBurstThreshold;
                bool isEndOfSustain = State.CurrentTick >= note.TickEnd;

                uint sustainTick = isBurst || isEndOfSustain ? note.TickEnd : State.CurrentTick;

                var mask = note.IsDisjoint ? note.DisjointMask : note.NoteMask;
                bool extendedSustainHold = (mask & State.ButtonMask) == mask;
                bool dropped = note.IsExtendedSustain ? !extendedSustainHold : !CanNoteBeHit(note);

                // If the sustain has not finished scoring, then we need to calculate the points
                if (!sustain.HasFinishedScoring)
                {
                    // Sustain has reached burst threshold, so all points have been given
                    if (isBurst)
                    {
                        sustain.HasFinishedScoring = true;
                    }

                    // Sustain has ended, so commit the points
                    if (dropped || isBurst)
                    {
                        double finalScore = CalculateSustainPoints(sustain, sustainTick);
                        AddScore((int) Math.Ceiling(finalScore));
                    }
                    else
                    {
                        EngineStats.PendingScore += (int) CalculateSustainPoints(sustain, sustainTick);
                    }
                }

                // Only remove the sustain if its dropped or has reached the final tick
                if (dropped || isEndOfSustain)
                {
                    ActiveSustains.RemoveAt(i);
                    i--;
                    OnSustainEnd?.Invoke(note, State.CurrentTime, sustain.HasFinishedScoring);
                }
            }

            UpdateStars();
            UpdateWhammyStarPower(isStarPowerSustainActive);
        }

        protected void UpdateWhammyStarPower(bool spSustainsActive)
        {
            if (spSustainsActive)
            {
                if (State.HasWhammied)
                {
                    // Rebase when beginning to SP whammy
                    if (!State.StarPowerWhammyTimer.IsEnabled)
                    {
                        RebaseProgressValues(State.CurrentTick);
                    }

                    StartTimer(ref State.StarPowerWhammyTimer, State.CurrentTime);

                    EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                    {
                        TimerName = "StarPowerWhammy",
                        TimerStarted = true,
                        TimerValue = State.StarPowerWhammyTimer.TimeThreshold,
                    });
                }
                else if (State.StarPowerWhammyTimer.IsExpired(State.CurrentTime))
                {
                    // No need to restart the timer, expiration is handled correctly in the gain calculation
                    // State.StarPowerWhammyTimer.Start(State.CurrentTime);

                    // Commit final whammy gain amount
                    UpdateProgressValues(State.CurrentTick);
                    RebaseProgressValues(State.CurrentTick);

                    // Stop whammy gain
                    State.StarPowerWhammyTimer.Disable();

                    EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                    {
                        TimerName = "StarPowerWhammy",
                        TimerStopped = true,
                        TimerValue = 0,
                    });
                }
            }
            // Rebase after SP whammy ends to commit the final amount to the base
            else if (State.StarPowerWhammyTimer.IsEnabled ||
                State.StarPowerWhammyTimer.IsExpired(State.CurrentTime))
            {
                RebaseProgressValues(State.CurrentTick);

                double remainingTime = Math.Max(State.StarPowerWhammyTimer.EndTime - State.CurrentTime, 0);
                State.StarPowerWhammyTimer.Disable();

                EventLogger.LogEvent(new TimerEngineEvent(State.CurrentTime)
                {
                    TimerName = "StarPowerWhammy",
                    TimerStopped = true,
                    TimerValue = remainingTime,
                });
            }
        }

        protected double CalculateSustainPoints(ActiveSustain sustain, uint tick)
        {
            uint scoreTick = Math.Clamp(tick, sustain.Note.Tick, sustain.Note.TickEnd);

            sustain.Note.SustainTicksHeld = scoreTick - sustain.Note.Tick;

            // Sustain points are awarded at a constant rate regardless of tempo
            // double deltaScore = CalculateBeatProgress(scoreTick, sustain.BaseTick, POINTS_PER_BEAT);
            double deltaScore = (scoreTick - sustain.BaseTick) / TicksPerSustainPoint;
            return sustain.BaseScore + (deltaScore * EngineStats.ScoreMultiplier);
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
                score += (int) Math.Ceiling(note.TickLength / TicksPerSustainPoint);

                // If a note is disjoint, each sustain is counted separately.
                if (note.IsDisjoint)
                {
                    foreach (var child in note.ChildNotes)
                    {
                        score += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                    }
                }
            }

            return score;
        }


        protected void ToggleFret(int fret, bool active)
        {
            State.FretMask = (byte) (active ? State.FretMask | (1 << fret) : State.FretMask & ~(1 << fret));
        }

        public bool IsFretHeld(GuitarAction fret)
        {
            return (State.FretMask & (1 << (int) fret)) != 0;
        }

        protected static bool IsFretInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.GreenFret or
                    GuitarAction.RedFret or
                    GuitarAction.YellowFret or
                    GuitarAction.BlueFret or
                    GuitarAction.OrangeFret or
                    GuitarAction.White3Fret => true,
                _ => false,
            };
        }

        protected static bool IsStrumInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.StrumUp or
                    GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}