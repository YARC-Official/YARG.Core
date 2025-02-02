using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngine<TNoteType, TEngineParams, TEngineStats> : BaseEngine
        where TNoteType : Note<TNoteType>
        where TEngineParams : BaseEngineParameters
        where TEngineStats : BaseStats, new()
    {
        // Max number of measures that SP will last when draining
        // SP draining is done based on measures
        protected const double STAR_POWER_MEASURE_AMOUNT = 1.0 / STAR_POWER_MAX_MEASURES;

        // Max number of beats that it takes to fill SP when gaining
        // SP gain from whammying is done based on beats
        protected const double STAR_POWER_BEAT_AMOUNT = 1.0 / STAR_POWER_MAX_BEATS;

        // Number of measures that SP phrases will grant when hit
        protected const int    STAR_POWER_PHRASE_MEASURE_COUNT = 2;
        protected const double STAR_POWER_PHRASE_AMOUNT = STAR_POWER_PHRASE_MEASURE_COUNT * STAR_POWER_MEASURE_AMOUNT;

        public delegate void NoteHitEvent(int noteIndex, TNoteType note);

        public delegate void NoteMissedEvent(int noteIndex, TNoteType note);

        public delegate void StarPowerPhraseHitEvent(TNoteType note);

        public delegate void StarPowerPhraseMissEvent(TNoteType note);

        public delegate void SustainStartEvent(TNoteType note);

        public delegate void SustainEndEvent(TNoteType note, double timeEnded, bool finished);

        public delegate void CountdownChangeEvent(double countdownLength, double endTime);

        public NoteHitEvent?    OnNoteHit;
        public NoteMissedEvent? OnNoteMissed;

        public StarPowerPhraseHitEvent?  OnStarPowerPhraseHit;
        public StarPowerPhraseMissEvent? OnStarPowerPhraseMissed;

        public SustainStartEvent? OnSustainStart;
        public SustainEndEvent?   OnSustainEnd;

        public CountdownChangeEvent? OnCountdownChange;

        protected SustainList<TNoteType> ActiveSustains = new(10);

        protected          int[]  StarScoreThresholds { get; }
        protected readonly double TicksPerSustainPoint;
        protected readonly uint   SustainBurstThreshold;

        public readonly TEngineStats EngineStats;

        protected readonly InstrumentDifficulty<TNoteType> Chart;

        protected readonly List<TNoteType> Notes;
        protected readonly TEngineParams   EngineParameters;

        public override BaseEngineParameters BaseParameters => EngineParameters;
        public override BaseStats            BaseStats      => EngineStats;

        protected BaseEngine(InstrumentDifficulty<TNoteType> chart, SyncTrack syncTrack,
            TEngineParams engineParameters, bool isChordSeparate, bool isBot)
            : base(syncTrack, isChordSeparate, isBot)
        {
            Chart = chart;
            Notes = Chart.Notes;
            EngineParameters = engineParameters;

            StarPowerWhammyTimer = new EngineTimer(engineParameters.StarPowerWhammyBuffer);

            EngineStats = new TEngineStats();
            Reset();

            EngineStats.ScoreMultiplier = 1;
            if (TreatChordAsSeparate)
            {
                foreach (var note in Notes)
                {
                    EngineStats.TotalNotes += GetNumberOfNotes(note);
                }
            }
            else
            {
                EngineStats.TotalNotes = Notes.Count;
            }

            EngineStats.TotalStarPowerPhrases = Chart.Phrases.Count((phrase) => phrase.Type == PhraseType.StarPower);

            TicksPerSustainPoint = Resolution / (double) POINTS_PER_BEAT;
            SustainBurstThreshold = Resolution / SUSTAIN_BURST_FRACTION;

            // This method should only rely on the `Notes` property (which is assigned above).
            // ReSharper disable once VirtualMemberCallInConstructor
            BaseScore = CalculateBaseScore();

            float[] multiplierThresholds = engineParameters.StarMultiplierThresholds;
            StarScoreThresholds = new int[multiplierThresholds.Length];
            for (int i = 0; i < multiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = (int) (BaseScore * multiplierThresholds[i]);
            }

            Solos = GetSoloSections();
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = CurrentTime;

            foreach (var sustain in ActiveSustains)
            {
                var burstTime = sustain.GetEndTime(SyncTrack, SustainBurstThreshold);
                var endTime = sustain.GetEndTime(SyncTrack, 0);

                var scaledDropLeniency = EngineParameters.SustainDropLeniency * EngineParameters.SongSpeed;
                var leniencyDropTime = sustain.LeniencyDropTime + scaledDropLeniency;

                if (sustain.IsLeniencyHeld && IsTimeBetween(leniencyDropTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing sustain (tick: {0}) leniency drop time at {1}", sustain.Note.Tick,
                        leniencyDropTime);
                    QueueUpdateTime(leniencyDropTime, "Sustain Leniency Drop");
                }

                // Burst time is for scoring, so that scoring finishes at the correct time
                if (IsTimeBetween(burstTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing sustain (tick: {0}) burst time at {1}", sustain.Note.Tick,
                        burstTime);
                    QueueUpdateTime(burstTime, "Sustain Burst");
                }

                // The true end of the sustain is for hit logic. Sustains are "kept" even after the burst ticks so must
                // also be handled.
                if (IsTimeBetween(endTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing sustain (tick: {0}) end time at {1}", sustain.Note.Tick,
                        endTime);
                    QueueUpdateTime(endTime, "Sustain End");
                }
            }

            for (int i = NoteIndex; i < Notes.Count; i++)
            {
                var note = Notes[i];

                var hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

                var noteFrontEnd = note.Time + EngineParameters.HitWindow.GetFrontEnd(hitWindow);
                var noteBackEnd = note.Time + EngineParameters.HitWindow.GetBackEnd(hitWindow);

                // Note will not reach front end yet
                if (nextTime < noteFrontEnd)
                {
                    //YargLogger.LogFormatTrace("Note {0} front end will not be reached at {1}", i, nextTime);
                    break;
                }

                if (!IsBot)
                {
                    // Earliest the note can be hit
                    if (IsTimeBetween(noteFrontEnd, previousTime, nextTime))
                    {
                        YargLogger.LogFormatTrace("Queuing note {0} front end hit time at {1}", i, noteFrontEnd);
                        QueueUpdateTime(noteFrontEnd, "Note Front End");
                    }
                }
                else
                {
                    if (IsTimeBetween(note.Time, previousTime, nextTime))
                    {
                        YargLogger.LogFormatTrace("Queuing bot note {0} at {1}", i, note.Time);
                        QueueUpdateTime(note.Time, "Bot Note Time");
                    }
                }

                // Note will not be out of time on the exact back end
                // So we increment the back end by 1 bit exactly
                // (essentially just 1 epsilon bigger)
                var noteBackEndIncrement = MathUtil.BitIncrement(noteBackEnd);

                if (IsTimeBetween(noteBackEndIncrement, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing note {0} back end miss time at {1}", i, noteBackEndIncrement);
                    QueueUpdateTime(noteBackEndIncrement, "Note Back End");
                }
            }

            if (StarPowerWhammyTimer.IsActive)
            {
                if (IsTimeBetween(StarPowerWhammyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing star power whammy end time at {0}",
                        StarPowerWhammyTimer.EndTime);
                    QueueUpdateTime(StarPowerWhammyTimer.EndTime, "Star Power Whammy End");
                }

                if (ActiveSustains.Count > 0)
                {
                    var syncIndex = CurrentSyncIndex;
                    var currentSync = CurrentSyncTrackState;

                    double maxTime = Math.Min(StarPowerWhammyTimer.EndTime, nextTime);

                    for (int i = syncIndex; i < SyncTrackChanges.Count; i++)
                    {
                        if (i + 1 < SyncTrackChanges.Count && SyncTrackChanges[i + 1].Time <= maxTime)
                        {
                            syncIndex = i;
                            currentSync = SyncTrackChanges[i];
                            break;
                        }
                    }

                    uint maxSpTick = GetStarPowerDrainTimeToTicks(maxTime, currentSync);

                    uint lastChartTick = SyncTrack.TimeToTick(previousTime);
                    uint lastSpTick = StarPowerTickPosition;

                    int spTickAmount = (int) BaseStats.StarPowerTickAmount;

                    for(uint spTick = StarPowerTickPosition + 1; spTick <= maxSpTick; spTick++)
                    {
                        var time = GetStarPowerDrainTickToTime(spTick, currentSync);
                        uint chartTick = SyncTrack.TimeToTick(time);

                        if (syncIndex + 1 < SyncTrackChanges.Count && SyncTrackChanges[syncIndex + 1].Time <= time)
                        {
                            syncIndex++;
                            currentSync = SyncTrackChanges[syncIndex];
                        }

                        uint whammyGain = chartTick - lastChartTick;
                        uint spDrain = spTick - lastSpTick;

                        spTickAmount += (int) whammyGain;

                        if (BaseStats.IsStarPowerActive)
                        {
                            spTickAmount -= (int) spDrain;

                            if (spTickAmount <= 0)
                            {
                                // Do we modify the engine state or just queue an update?
                                // Below it will check the end time anyway and queue an update
                                YargLogger.LogFormatDebug("Simulated SP gain/drain and found an end time at {0}", time);

                                StarPowerTickEndPosition = spTick;
                                StarPowerEndTime = time;
                                break;
                            }
                        }

                        if(spTickAmount > TicksPerFullSpBar)
                        {
                            spTickAmount = (int) TicksPerFullSpBar;
                            YargLogger.LogFormatTrace("Simulated SP gain/drain and found a full SP bar at {0}", time);
                            QueueUpdateTime(time, "SP Bar Cap Time");
                        }

                        lastChartTick = chartTick;
                        lastSpTick = spTick;
                    }
                }
            }

            if (BaseStats.IsStarPowerActive)
            {
                if (IsTimeBetween(StarPowerEndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing Star Power End Time at {0}", StarPowerEndTime);
                    QueueUpdateTime(StarPowerEndTime, "SP End Time");
                }
            }
            else
            {
                if (StarPowerWhammyTimer.IsActive)
                {
                    var nextTimeTick = SyncTrack.TimeToTick(nextTime);
                    var tickDelta = nextTimeTick - CurrentTick;

                    var ticksAfterWhammy = BaseStats.StarPowerTickAmount + tickDelta;

                    if (ticksAfterWhammy >= TicksPerHalfSpBar)
                    {
                        var ticksToHalfBar = TicksPerHalfSpBar - BaseStats.StarPowerTickAmount;
                        var timeOfHalfBar = SyncTrack.TickToTime(CurrentTick + ticksToHalfBar);

                        if (IsTimeBetween(timeOfHalfBar, previousTime, nextTime))
                        {
                            YargLogger.LogFormatTrace("Queuing star power half bar time at {0}",
                                timeOfHalfBar);
                            QueueUpdateTime(timeOfHalfBar, "Star Power Half Bar");
                        }
                    }
                }
            }

            if (CurrentWaitCountdownIndex < WaitCountdowns.Count)
            {
                // Queue updates for countdown start/end/change

                if (IsWaitCountdownActive)
                {
                    var currentCountdown = WaitCountdowns[CurrentWaitCountdownIndex];
                    double deactivateTime = currentCountdown.DeactivateTime;

                    if (IsTimeBetween(deactivateTime, previousTime, nextTime))
                    {
                        YargLogger.LogFormatTrace("Queuing countdown {0} deactivation at {1}",
                            CurrentWaitCountdownIndex, deactivateTime);
                        QueueUpdateTime(deactivateTime, "Deactivate Countdown");
                    }
                }
                else
                {
                    int nextCountdownIndex;

                    if (previousTime < WaitCountdowns[CurrentWaitCountdownIndex].Time)
                    {
                        // No countdowns are currently displayed
                        // CurrentWaitCountdownIndex has already been incremented for the next countdown
                        nextCountdownIndex = CurrentWaitCountdownIndex;
                    }
                    else
                    {
                        // A countdown is currently onscreen, but is past its deactivation time and is fading out
                        // CurrentWaitCountdownIndex will not be incremented until the progress bar no longer needs updating
                        nextCountdownIndex = CurrentWaitCountdownIndex + 1;
                    }

                    if (nextCountdownIndex < WaitCountdowns.Count)
                    {
                        double nextCountdownStartTime = WaitCountdowns[nextCountdownIndex].Time;

                        if (IsTimeBetween(nextCountdownStartTime, previousTime, nextTime))
                        {
                            YargLogger.LogFormatTrace("Queuing countdown {0} start time at {1}", nextCountdownIndex,
                                nextCountdownStartTime);
                            QueueUpdateTime(nextCountdownStartTime, "Activate Countdown");
                        }
                    }
                }
            }
        }

        protected override void UpdateTimeVariables(double time)
        {
            if (time < CurrentTime)
            {
                YargLogger.FailFormat("Time cannot go backwards! Current time: {0}, new time: {1}", CurrentTime,
                    time);
            }

            LastUpdateTime = CurrentTime;
            LastTick = CurrentTick;

            CurrentTime = time;
            CurrentTick = GetCurrentTick(time);

            while (NextSyncIndex < SyncTrackChanges.Count && CurrentTick >= SyncTrackChanges[NextSyncIndex].Tick)
            {
                CurrentSyncIndex++;
            }

            // Only check for WaitCountdowns in this chart if there are any remaining
            if (CurrentWaitCountdownIndex < WaitCountdowns.Count)
            {
                var currentCountdown = WaitCountdowns[CurrentWaitCountdownIndex];

                if (time >= currentCountdown.Time)
                {
                    if (time < currentCountdown.DeactivateTime)
                    {
                        // This countdown should be displayed onscreen
                        if (!IsWaitCountdownActive)
                        {
                            // Entered new countdown window
                            IsWaitCountdownActive = true;
                            YargLogger.LogFormatTrace("Countdown {0} activated at time {1}. Expected time: {2}", CurrentWaitCountdownIndex, time, currentCountdown.Time);
                        }

                        UpdateCountdown(currentCountdown.TimeLength, currentCountdown.TimeEnd);
                    }
                    else
                    {
                        if (IsWaitCountdownActive)
                        {
                            IsWaitCountdownActive = false;
                            YargLogger.LogFormatTrace("Countdown {0} deactivated at time {1}. Expected time: {2}", CurrentWaitCountdownIndex, time, currentCountdown.DeactivateTime);
                        }

                        CurrentWaitCountdownIndex++;
                    }
                }
            }
        }

        public override void AllowStarPower(bool isAllowed)
        {
            if (isAllowed == StarPowerIsAllowed)
            {
                return;
            }

            StarPowerIsAllowed = isAllowed;

            foreach (var note in Notes)
            {
                if (isAllowed)
                {
                    note.ResetFlags();
                }
                else if (note.IsStarPower)
                {
                    note.Flags &= ~NoteFlags.StarPower;
                    foreach (var childNote in note.ChildNotes)
                    {
                        childNote.Flags &= ~NoteFlags.StarPower;
                    }
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            base.Reset();

            InputQueue.Clear();

            EngineStats.Reset();

            StarPowerWhammyTimer.Disable();

            foreach (var note in Notes)
            {
                note.ResetNoteState();

                if (!StarPowerIsAllowed && note.IsStarPower)
                {
                    note.Flags &= ~NoteFlags.StarPower;
                    foreach (var childNote in note.ChildNotes)
                    {
                        childNote.Flags &= ~NoteFlags.StarPower;
                    }
                }
            }

            foreach (var solo in Solos)
            {
                solo.NotesHit = 0;
                solo.SoloBonus = 0;
            }
        }

        protected abstract void CheckForNoteHit();

        /// <summary>
        /// Checks if the given note can be hit with the current input
        /// </summary>
        /// <param name="note">The Note to attempt to hit.</param>
        /// <returns>True if note can be hit. False otherwise.</returns>
        protected abstract bool CanNoteBeHit(TNoteType note);

        protected abstract bool CanSustainHold(TNoteType note);

        protected virtual void HitNote(TNoteType note)
        {
            if (note.ParentOrSelf.WasFullyHitOrMissed())
            {
                AdvanceToNextNote(note);
            }
        }

        protected virtual void MissNote(TNoteType note)
        {
            if (note.ParentOrSelf.WasFullyHitOrMissed())
            {
                AdvanceToNextNote(note);
            }
        }

        protected bool SkipPreviousNotes(TNoteType current)
        {
            bool skipped = false;
            var prevNote = current.PreviousNote;
            while (prevNote is not null && !prevNote.WasFullyHitOrMissed())
            {
                skipped = true;
                YargLogger.LogFormatTrace("Missed note (Index: {0}) ({1}) due to note skip at {2}", NoteIndex, prevNote.IsParent ? "Parent" : "Child", CurrentTime);
                MissNote(prevNote);

                if (TreatChordAsSeparate)
                {
                    foreach (var child in prevNote.ChildNotes)
                    {
                        YargLogger.LogFormatTrace("Missed note (Index: {0}) ({1}) due to note skip at {2}", NoteIndex, child.IsParent ? "Parent" : "Child", CurrentTime);
                        MissNote(child);
                    }
                }

                prevNote = prevNote.PreviousNote;
            }

            return skipped;
        }

        protected abstract void AddScore(TNoteType note);

        protected void AddScore(int score)
        {
            int scoreMultiplier = score * EngineStats.ScoreMultiplier;

            // scoreMultiplier includes combo+star power score
            EngineStats.CommittedScore += scoreMultiplier;

            if (EngineStats.IsStarPowerActive)
            {
                // Amount of points just from Star Power is half of the current multiplier (8x total -> 4x SP points)
                var spScore = scoreMultiplier / 2;

                EngineStats.StarPowerScore += spScore;

                // Subtract score from the note that was just hit to get the multiplier points
                EngineStats.MultiplierScore += spScore - score;
            }
            else
            {
                EngineStats.MultiplierScore += scoreMultiplier - score;
            }
            UpdateStars();
        }

        protected virtual void UpdateSustains()
        {
            EngineStats.PendingScore = 0;

            bool isStarPowerSustainActiveRightNow = false;
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                ref var sustain = ref ActiveSustains[i];
                var note = sustain.Note;

                isStarPowerSustainActiveRightNow |= note.IsStarPower;

                // If we're close enough to the end of the sustain, finish it
                // Provides leniency for sustains with no gap (and just in general)
                bool isBurst;

                // Sustain is too short for a burst
                if (SustainBurstThreshold > note.TickLength)
                {
                    isBurst = CurrentTick >= note.Tick;
                }
                else
                {
                    isBurst = CurrentTick >= note.TickEnd - SustainBurstThreshold;
                }

                bool isEndOfSustain = CurrentTick >= note.TickEnd;

                uint sustainTick = isBurst || isEndOfSustain ? note.TickEnd : CurrentTick;

                bool dropped = false;

                if(!CanSustainHold(note))
                {
                    // Currently beind held by sustain drop leniency
                    if (sustain.IsLeniencyHeld)
                    {
                        if (CurrentTime >= sustain.LeniencyDropTime + EngineParameters.SustainDropLeniency * EngineParameters.SongSpeed)
                        {
                            dropped = true;
                            YargLogger.LogFormatTrace("Dropping sustain using leniency time at {0}", CurrentTime);
                        }
                    }
                    else
                    {
                        sustain.IsLeniencyHeld = true;
                        sustain.LeniencyDropTime = CurrentTime;
                    }
                }
                else
                {
                    sustain.IsLeniencyHeld = false;
                }

                // If the sustain has not finished scoring, then we need to calculate the points
                if (!sustain.HasFinishedScoring)
                {
                    // Sustain has reached burst threshold, so all points have been given
                    if (isBurst || isEndOfSustain)
                    {
                        sustain.HasFinishedScoring = true;
                    }

                    // Sustain has ended, so commit the points
                    if (dropped || isBurst || isEndOfSustain)
                    {
                        YargLogger.LogFormatTrace("Finished scoring sustain ({0}) at {1} (dropped: {2}, burst: {3})",
                            sustain.Note.Tick, CurrentTime, dropped, isBurst);

                        double finalScore = CalculateSustainPoints(ref sustain, sustainTick);
                        var points = (int) Math.Ceiling(finalScore);

                        AddScore(points);
                        ulong timeAsUlong = UnsafeExtensions.DoubleToUInt64Bits(CurrentTime);
                        ulong baseScoreAsUlong = UnsafeExtensions.DoubleToUInt64Bits(sustain.BaseScore);
                        YargLogger.LogFormatTrace("Added {0} points for end of sustain at {1} (0x{2}). Base Score/Tick: {3} (0x{4}), {5}", points, CurrentTime, timeAsUlong.ToString("X"), sustain.BaseScore, baseScoreAsUlong.ToString("X"), sustain.BaseTick);

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
                        double score = CalculateSustainPoints(ref sustain, sustainTick);

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
        }

        protected virtual void StartSustain(TNoteType note)
        {
            var sustain = new ActiveSustain<TNoteType>(note);

            ActiveSustains.Add(sustain);

            YargLogger.LogFormatTrace("Started sustain at {0} (tick len: {1}, time len: {2})", CurrentTime, note.TickLength, note.TimeLength);

            OnSustainStart?.Invoke(note);
        }

        protected virtual void EndSustain(int sustainIndex, bool dropped, bool isEndOfSustain)
        {
            var sustain = ActiveSustains[sustainIndex];
            YargLogger.LogFormatTrace("Ended sustain ({0}) at {1} (dropped: {2}, end: {3})", sustain.Note.Tick, CurrentTime, dropped, isEndOfSustain);
            ActiveSustains.RemoveAt(sustainIndex);

            OnSustainEnd?.Invoke(sustain.Note, CurrentTime, sustain.HasFinishedScoring);
        }

        protected override void UpdateStarPower()
        {
            bool isStarPowerSustainActive = false;
            foreach (var sustain in ActiveSustains)
            {
                isStarPowerSustainActive |= sustain.Note.IsStarPower;
            }

            if (isStarPowerSustainActive && StarPowerWhammyTimer.IsActive)
            {
                var whammyTicks = CurrentTick - LastTick;

                // Just started whammying, award 1 tick
                if (!LastWhammyTimerState)
                {
                    whammyTicks = 1;
                }

                // Don't cap until drain has been calculated
                BaseStats.StarPowerTickAmount += whammyTicks;

                BaseStats.TotalStarPowerTicks += whammyTicks;
                BaseStats.StarPowerWhammyTicks += whammyTicks;

                if (BaseStats.IsStarPowerActive)
                {
                    UpdateStarPowerEnds();
                }

                BaseStats.TotalStarPowerBarsFilled = (double) BaseStats.TotalStarPowerTicks / TicksPerFullSpBar;
                YargLogger.LogFormatTrace("Gained {0} whammy ticks this update (Total: {1}), {2} sustains active. SP right now: {3}", whammyTicks, EngineStats.StarPowerWhammyTicks, ActiveSustains.Count, isStarPowerSustainActive);
            }

            PreviousStarPowerTickPosition = StarPowerTickPosition;
            StarPowerTickPosition = GetStarPowerDrainTimeToTicks(CurrentTime, CurrentSyncTrackState);

            if (BaseStats.IsStarPowerActive)
            {
                var drain = StarPowerTickPosition - PreviousStarPowerTickPosition;
                if ((int) BaseStats.StarPowerTickAmount - drain <= 0)
                {
                    BaseStats.StarPowerTickAmount = 0;
                }
                else
                {
                    BaseStats.StarPowerTickAmount -= drain;

                    YargLogger.LogFormatTrace("Drained {0} ticks of SP this update. New SP tick amount: {1}. Current SP Tick: {2}, Last: {3}", drain, BaseStats.StarPowerTickAmount, StarPowerTickPosition, PreviousStarPowerTickPosition);
                }

                double spTimeDelta = CurrentTime - StarPowerActivationTime;
                BaseStats.TimeInStarPower = spTimeDelta + BaseTimeInStarPower;
                YargLogger.LogFormatTrace("Updated Star Power Time to {0} (delta: {1}, base: {2})", BaseStats.TimeInStarPower, spTimeDelta, BaseTimeInStarPower);
            }

            // Limit amount of ticks to a full bar.
            if(BaseStats.StarPowerTickAmount > TicksPerFullSpBar)
            {
                BaseStats.StarPowerTickAmount = TicksPerFullSpBar;

                if (BaseStats.IsStarPowerActive)
                {
                    UpdateStarPowerEnds();
                    YargLogger.LogFormatTrace("Clamped SP. New end tick and time: {0}, {1}", StarPowerTickEndPosition, StarPowerEndTime);
                }
            }

            LastWhammyTimerState = StarPowerWhammyTimer.IsActive;

            if (BaseStats is { IsStarPowerActive: true, StarPowerTickAmount: 0 })
            {
                ReleaseStarPower();
            }

            if (IsStarPowerInputActive && CanStarPowerActivate)
            {
                ActivateStarPower();
            }

            if (StarPowerWhammyTimer.IsActive && StarPowerWhammyTimer.IsExpired(CurrentTime))
            {
                StarPowerWhammyTimer.Disable();
                YargLogger.LogFormatTrace("Disabling whammy timer at {0}", CurrentTime);
            }
        }

        protected void UpdateStars()
        {
            // Update which star we're on
            while (CurrentStarIndex < StarScoreThresholds.Length &&
                EngineStats.StarScore > StarScoreThresholds[CurrentStarIndex])
            {
                CurrentStarIndex++;
            }

            // Calculate current star progress
            float progress = 0f;
            if (CurrentStarIndex < StarScoreThresholds.Length)
            {
                int previousPoints = CurrentStarIndex > 0 ? StarScoreThresholds[CurrentStarIndex - 1] : 0;
                int nextPoints = StarScoreThresholds[CurrentStarIndex];
                progress = YargMath.InverseLerpF(previousPoints, nextPoints, EngineStats.StarScore);
            }

            EngineStats.Stars = CurrentStarIndex + progress;
        }

        protected virtual void StripStarPower(TNoteType? note)
        {
            if (note is null || !note.IsStarPower)
            {
                return;
            }

            // Strip star power from the note and all its children
            note.Flags &= ~NoteFlags.StarPower;
            foreach (var childNote in note.ChildNotes)
            {
                childNote.Flags &= ~NoteFlags.StarPower;
            }

            // Look back until finding the start of the phrase
            if (!note.IsStarPowerStart)
            {
                var prevNote = note.PreviousNote;
                while (prevNote is not null && prevNote.IsStarPower)
                {
                    prevNote.Flags &= ~NoteFlags.StarPower;
                    foreach (var childNote in prevNote.ChildNotes)
                    {
                        childNote.Flags &= ~NoteFlags.StarPower;
                    }

                    if (prevNote.IsStarPowerStart)
                    {
                        break;
                    }

                    prevNote = prevNote.PreviousNote;
                }
            }

            // Look forward until finding the end of the phrase
            if (!note.IsStarPowerEnd)
            {
                var nextNote = note.NextNote;
                while (nextNote is not null && nextNote.IsStarPower)
                {
                    nextNote.Flags &= ~NoteFlags.StarPower;
                    foreach (var childNote in nextNote.ChildNotes)
                    {
                        childNote.Flags &= ~NoteFlags.StarPower;
                    }

                    if (nextNote.IsStarPowerEnd)
                    {
                        break;
                    }

                    nextNote = nextNote.NextNote;
                }
            }

            OnStarPowerPhraseMissed?.Invoke(note);
        }

        protected virtual uint CalculateStarPowerGain(uint tick) => tick - LastTick;

        protected void AwardStarPower(TNoteType note)
        {
            GainStarPower(TicksPerQuarterSpBar);

            OnStarPowerPhraseHit?.Invoke(note);
        }

        protected void StartSolo()
        {
            if (CurrentSoloIndex >= Solos.Count)
            {
                return;
            }

            IsSoloActive = true;
            OnSoloStart?.Invoke(Solos[CurrentSoloIndex]);
        }

        protected void EndSolo()
        {
            if (!IsSoloActive)
            {
                return;
            }

            var currentSolo = Solos[CurrentSoloIndex];

            double soloPercentage = currentSolo.NotesHit / (double) currentSolo.NoteCount;

            if (soloPercentage < 0.6)
            {
                currentSolo.SoloBonus = 0;
            }
            else
            {
                double multiplier = Math.Clamp((soloPercentage - 0.6) / 0.4, 0, 1);

                // Old engine says this is 200 *, but I'm not sure that's right?? Isn't it 2x the note's worth, not 4x?
                double points = 100 * currentSolo.NotesHit * multiplier;

                // Round down to nearest 50 (kinda just makes sense I think?)
                points -= points % 50;

                currentSolo.SoloBonus = (int) points;
            }

            EngineStats.SoloBonuses += currentSolo.SoloBonus;

            IsSoloActive = false;

            OnSoloEnd?.Invoke(Solos[CurrentSoloIndex]);
            CurrentSoloIndex++;
        }

        protected override void UpdateProgressValues(uint tick)
        {
            base.UpdateProgressValues(tick);

            EngineStats.PendingScore = 0;
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                ref var sustain = ref ActiveSustains[i];
                EngineStats.PendingScore += (int) CalculateSustainPoints(ref sustain, tick);
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
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                ref var sustain = ref ActiveSustains[i];
                // Don't rebase sustains that haven't started yet
                if (baseTick < sustain.BaseTick)
                {
                    YargLogger.AssertFormat(baseTick < sustain.Note.Tick,
                        "Sustain base tick cannot go backwards! Attempted to go from {0} to {1}",
                        sustain.BaseTick, baseTick);

                    continue;
                }

                double sustainScore = CalculateSustainPoints(ref sustain, baseTick);

                sustain.BaseTick = Math.Clamp(baseTick, sustain.Note.Tick, sustain.Note.TickEnd);
                sustain.BaseScore = sustainScore;
                EngineStats.PendingScore += (int) sustainScore;
            }
        }

        protected void UpdateCountdown(double countdownLength, double endTime)
        {
            OnCountdownChange?.Invoke(countdownLength, endTime);
        }

        public sealed override (double FrontEnd, double BackEnd) CalculateHitWindow()
        {
            var maxWindow = EngineParameters.HitWindow.MaxWindow;

            if (NoteIndex >= Notes.Count)
            {
                return (EngineParameters.HitWindow.GetFrontEnd(maxWindow),
                    EngineParameters.HitWindow.GetBackEnd(maxWindow));
            }

            var noteDistance = GetAverageNoteDistance(Notes[NoteIndex]);
            var hitWindow = EngineParameters.HitWindow.CalculateHitWindow(noteDistance);

            return (EngineParameters.HitWindow.GetFrontEnd(hitWindow),
                EngineParameters.HitWindow.GetBackEnd(hitWindow));
        }

        /// <summary>
        /// Calculates the base score of the chart, which can be used to calculate star thresholds.
        /// </summary>
        /// <remarks>
        /// Please be mindful that this virtual method is called in the constructor of
        /// <see cref="BaseEngine{TNoteType,TEngineParams,TEngineStats,TEngineState}"/>.
        /// <b>ONLY</b> use the <see cref="Notes"/> property to calculate this.
        /// </remarks>
        protected abstract int CalculateBaseScore();

        protected bool IsNoteInWindow(TNoteType note) => IsNoteInWindow(note, out _);

        protected bool IsNoteInWindow(TNoteType note, double time) =>
            IsNoteInWindow(note, out _, time);

        protected bool IsNoteInWindow(TNoteType note, out bool missed) =>
            IsNoteInWindow(note, out missed, CurrentTime);

        protected bool IsNoteInWindow(TNoteType note, out bool missed, double time)
        {
            missed = false;

            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            double frontend = EngineParameters.HitWindow.GetFrontEnd(hitWindow);
            double backend = EngineParameters.HitWindow.GetBackEnd(hitWindow);

            // Time has not reached the front end of this note
            if (time < note.Time + frontend)
            {
                return false;
            }

            // Time has surpassed the back end of this note
            if (time > note.Time + backend)
            {
                missed = true;
                return false;
            }

            return true;
        }

        protected double CalculateSustainPoints(ref ActiveSustain<TNoteType> sustain, uint tick)
        {
            uint scoreTick = Math.Clamp(tick, sustain.Note.Tick, sustain.Note.TickEnd);

            sustain.Note.SustainTicksHeld = scoreTick - sustain.Note.Tick;

            // Sustain points are awarded at a constant rate regardless of tempo
            // double deltaScore = CalculateBeatProgress(scoreTick, sustain.BaseTick, POINTS_PER_BEAT);
            double deltaScore = (scoreTick - sustain.BaseTick) / TicksPerSustainPoint;
            return sustain.BaseScore + deltaScore;
        }

        private void AdvanceToNextNote(TNoteType note)
        {
            NoteIndex++;
            ReRunHitLogic = true;
        }

        public double GetAverageNoteDistance(TNoteType note)
        {
            double previousToCurrent;
            double currentToNext = EngineParameters.HitWindow.MaxWindow / 2;

            if (note.NextNote is not null)
            {
                currentToNext = (note.NextNote.Time - note.Time) / 2;
            }

            if (note.PreviousNote is not null)
            {
                previousToCurrent = (note.Time - note.PreviousNote.Time) / 2;
            }
            else
            {
                previousToCurrent = currentToNext;
            }

            return previousToCurrent + currentToNext;
        }

        private List<SoloSection> GetSoloSections()
        {
            var soloSections = new List<SoloSection>();

            if (Notes.Count > 0 && Notes[0] is { IsSolo: true, IsSoloEnd: false })
            {
                Notes[0].ActivateFlag(NoteFlags.SoloStart);
            }

            if (Notes.Count > 0 && Notes[^1].IsSolo)
            {
                Notes[^1].ActivateFlag(NoteFlags.SoloEnd);
            }

            for (int i = 0; i < Notes.Count; i++)
            {
                var curr = Notes[i];
                if (curr.IsSoloStart)
                {
                    int soloNoteCount = 0;
                    uint start = curr.Tick;
                    while (true)
                    {
                        soloNoteCount += GetNumberOfNotes(curr);
                        if (curr.IsSoloEnd || i + 1 == Notes.Count)
                        {
                            break;
                        }
                        curr = Notes[++i];
                    }
                    soloSections.Add(new SoloSection(start, curr.Tick, soloNoteCount));
                }
            }

            return soloSections;
        }

        protected void GetWaitCountdowns(List<TNoteType> notes)
        {
            WaitCountdowns = new List<WaitCountdown>();
            for (int i = 0; i < notes.Count; i++)
            {
                // Compare the note at the current index against the previous note
                double noteOneTimeEnd = 0;
                uint noteOneTickEnd = 0;

                if (i > 0) {
                    Note<TNoteType> noteOne = notes[i-1];
                    noteOneTimeEnd = noteOne.TimeEnd;
                    noteOneTickEnd = noteOne.TickEnd;
                }

                Note<TNoteType> noteTwo = notes[i];

                if (noteTwo.Time - noteOneTimeEnd >= WaitCountdown.MIN_SECONDS)
                {
                    // Distance between these two notes is over the threshold
                    // Create a WaitCountdown instance to reference at runtime
                    var newCountdown = new WaitCountdown(noteOneTimeEnd, noteTwo.Time - noteOneTimeEnd, noteOneTickEnd, noteTwo.Tick - noteOneTickEnd);

                    WaitCountdowns.Add(newCountdown);
                    YargLogger.LogFormatTrace("Created a WaitCountdown at time {0} of {1} seconds in length", newCountdown.Time, newCountdown.TimeLength);
                }
            }
        }
    }
}