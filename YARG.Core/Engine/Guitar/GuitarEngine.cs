using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarEngineParameters,
        GuitarStats, GuitarEngineState>
    {
        protected sealed class ActiveSustain
        {
            public GuitarNote Note;
            public uint       BaseTick;
            public double     BaseScore;

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

        protected uint LastWhammyTick;

        protected GuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
            State.Initialize(engineParameters);
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = State.CurrentTime;

            foreach (var sustain in ActiveSustains)
            {
                var burstTime = sustain.GetEndTime(SyncTrack, SustainBurstThreshold);
                var endTime = sustain.GetEndTime(SyncTrack, 0);

                // Burst time is for scoring, so that scoring finishes at the correct time
                if (IsTimeBetween(burstTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing sustain (mask: {0}) burst time at {1}", sustain.Note.NoteMask,
                        burstTime);
                    QueueUpdateTime(burstTime, "Sustain Burst");
                }

                // The true end of the sustain is for hit logic. Sustains are "kept" even after the burst ticks so must
                // also be handled.
                if (IsTimeBetween(endTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing sustain (mask: {0}) end time at {1}", sustain.Note.NoteMask,
                        endTime);
                    QueueUpdateTime(endTime, "Sustain End");
                }
            }

            // Check all timers
            if (State.HopoLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(State.HopoLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing hopo leniency end time at {0}", State.HopoLeniencyTimer.EndTime);
                    QueueUpdateTime(State.HopoLeniencyTimer.EndTime, "HOPO Leniency End");
                }
            }

            if (State.StrumLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(State.StrumLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing strum leniency end time at {0}",
                        State.StrumLeniencyTimer.EndTime);
                    QueueUpdateTime(State.StrumLeniencyTimer.EndTime, "Strum Leniency End");
                }
            }

            if (State.StarPowerWhammyTimer.IsActive)
            {
                if (IsTimeBetween(State.StarPowerWhammyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing star power whammy end time at {0}",
                        State.StarPowerWhammyTimer.EndTime);
                    QueueUpdateTime(State.StarPowerWhammyTimer.EndTime, "Star Power Whammy End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            byte buttons = State.ButtonMask;
            ActiveSustains.Clear();

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                State.ButtonMask = buttons;
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

            YargLogger.LogFormatTrace("Overstrummed at {0}", State.CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), State.CurrentTime);
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
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Fret, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetHitState(true, true);

            // Detect if the last note(s) were skipped
            bool skipped = SkipPreviousNotes(note);

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
                foreach (var chordNote in note.AllNotes)
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

            OnNoteHit?.Invoke(State.NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Fret, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

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

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            base.MissNote(note);
        }

        protected override void AddScore(GuitarNote note)
        {
            int notePoints = POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
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
            RebaseSustains(baseTick);
        }

        protected void RebaseSustains(uint baseTick)
        {
            EngineStats.PendingScore = 0;
            foreach (var sustain in ActiveSustains)
            {
                // Don't rebase sustains that haven't started yet
                if (baseTick < sustain.BaseTick)
                {
                    YargLogger.AssertFormat(baseTick < sustain.Note.Tick,
                        "Sustain base tick cannot go backwards! Attempted to go from {0} to {1}",
                        sustain.BaseTick, baseTick);

                    continue;
                }

                double sustainScore = CalculateSustainPoints(sustain, baseTick);

                sustain.BaseTick = Math.Clamp(baseTick, sustain.Note.Tick, sustain.Note.TickEnd);
                sustain.BaseScore = sustainScore;
                EngineStats.PendingScore += (int) sustainScore;
            }
        }

        protected void StartSustain(GuitarNote note)
        {
            //return;
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var activeSustain = ActiveSustains[i];

                // open notes NEED to have a bit in the bit mask because this is not going to work properly in some cases
                if ((activeSustain.Note.NoteMask & note.NoteMask) != 0 || (activeSustain.Note.NoteMask == 0 && note.NoteMask == 0))
                {
                    EndSustain(i, true, State.CurrentTick >= activeSustain.Note.TickEnd);
                    i--;
                }
            }

            if (ActiveSustains.Count == 0)
            {
                LastWhammyTick = State.CurrentTick;
            }

            var sustain = new ActiveSustain(note);

            ActiveSustains.Add(sustain);

            YargLogger.LogFormatTrace("Started sustain at {0} (tick len: {1}, time len: {2})", State.CurrentTime, note.TickLength, note.TimeLength);

            OnSustainStart?.Invoke(note);
        }

        protected void EndSustain(int sustainIndex, bool dropped, bool isEndOfSustain)
        {
            var sustain = ActiveSustains[sustainIndex];
            YargLogger.LogFormatTrace("Ended sustain at {0} (dropped: {1}, end: {2})", State.CurrentTime, dropped, isEndOfSustain);
            ActiveSustains.RemoveAt(sustainIndex);

            OnSustainEnd?.Invoke(sustain.Note, State.CurrentTime, sustain.HasFinishedScoring);
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
                bool isBurst = (int) State.CurrentTick >= note.TickEnd - SustainBurstThreshold;
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
                        YargLogger.LogFormatTrace("Finished scoring sustain at {0} (dropped: {1}, burst: {2})", State.CurrentTime, dropped, isBurst);

                        double finalScore = CalculateSustainPoints(sustain, sustainTick);
                        var points = (int) Math.Ceiling(finalScore);

                        AddScore(points);

                        // SustainPoints must include the multiplier, but NOT the star power multiplier
                        int sustainPoints = points * EngineStats.ScoreMultiplier;
                        if (EngineStats.IsStarPowerActive)
                        {
                            sustainPoints /= 2;
                        }

                        EngineStats.SustainScore += sustainPoints;
                    }
                    else
                    {
                        double score = CalculateSustainPoints(sustain, sustainTick);

                        var sustainPoints = (int) Math.Ceiling(score);

                        // It's ok to use multiplier here because PendingScore is only temporary to show the correct
                        // score on the UI.
                        EngineStats.PendingScore += sustainPoints * EngineStats.ScoreMultiplier;
                    }
                }

                // Only remove the sustain if its dropped or has reached the final tick
                if (dropped || isEndOfSustain)
                {
                    EndSustain(i, dropped, isEndOfSustain);
                    i--;
                }
            }

            UpdateStars();
            if (isStarPowerSustainActive && State.StarPowerWhammyTimer.IsActive)
            {
                var whammyTicks = State.CurrentTick - LastWhammyTick;

                GainStarPower(whammyTicks);
                EngineStats.WhammyTicks += whammyTicks;

                LastWhammyTick = State.CurrentTick;
            }

            // Whammy is disabled after sustains are updated.
            // This is because all the ticks that have accumulated will have been accounted for when it is disabled.
            // Whereas disabling it before could mean there are some ticks which should have been whammied but weren't.
            if (State.StarPowerWhammyTimer.IsActive && State.StarPowerWhammyTimer.IsExpired(State.CurrentTime))
            {
                State.StarPowerWhammyTimer.Disable();
            }
        }

        protected double CalculateSustainPoints(ActiveSustain sustain, uint tick)
        {
            uint scoreTick = Math.Clamp(tick, sustain.Note.Tick, sustain.Note.TickEnd);

            sustain.Note.SustainTicksHeld = scoreTick - sustain.Note.Tick;

            // Sustain points are awarded at a constant rate regardless of tempo
            // double deltaScore = CalculateBeatProgress(scoreTick, sustain.BaseTick, POINTS_PER_BEAT);
            double deltaScore = (scoreTick - sustain.BaseTick) / TicksPerSustainPoint;
            return sustain.BaseScore + deltaScore;
        }

        public override void SetSpeed(double speed)
        {
            base.SetSpeed(speed);
            State.HopoLeniencyTimer.SetSpeed(speed);
            State.StrumLeniencyTimer.SetSpeed(speed);
            State.StarPowerWhammyTimer.SetSpeed(speed);
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
            State.ButtonMask = (byte) (active ? State.ButtonMask | (1 << fret) : State.ButtonMask & ~(1 << fret));
        }

        public bool IsFretHeld(GuitarAction fret)
        {
            return (State.ButtonMask & (1 << (int) fret)) != 0;
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
