using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public abstract class BaseEngine
    {
        protected const int POINTS_PER_NOTE     = 50;
        protected const int POINTS_PER_PRO_NOTE = POINTS_PER_NOTE + 10;
        protected const int POINTS_PER_BEAT     = 25;

        // Max number of measures that SP will last when draining
        // SP draining is done based on measures
        protected const int STAR_POWER_MAX_MEASURES = 8;

        // Beat fraction to use for the sustain burst threshold
        protected const int SUSTAIN_BURST_FRACTION = 4;

        public delegate void StarPowerStatusEvent(bool active);
        public delegate void SoloStartEvent(SoloSection soloSection);
        public delegate void SoloEndEvent(SoloSection soloSection);
        public delegate void ComboResetEvent();
        public delegate void ComboIncrementEvent(int amount);

        public delegate void UnisonBonusAwardedEvent();
        public StarPowerStatusEvent? OnStarPowerStatus;
        public SoloStartEvent?       OnSoloStart;
        public SoloEndEvent?         OnSoloEnd;
        public ComboResetEvent?      OnComboReset;
        public ComboIncrementEvent?  OnComboIncrement;
        public UnisonBonusAwardedEvent? OnUnisonBonusAwarded;

        public bool CanStarPowerActivate => BaseStats.StarPowerTickAmount >= TicksPerHalfSpBar;

        public int BaseScore { get; protected set; }

        public abstract BaseEngineParameters BaseParameters { get; }
        public abstract BaseStats            BaseStats      { get; }

        protected bool StarPowerIsAllowed = true;

        protected readonly SyncTrack SyncTrack;

        public readonly uint TicksPerQuarterSpBar;
        public readonly uint TicksPerHalfSpBar;
        public readonly uint TicksPerFullSpBar;

        protected List<SoloSection> Solos = new();

        protected List<WaitCountdown> WaitCountdowns = new();

        protected readonly Queue<GameInput> InputQueue = new();

        private readonly List<EngineFrameUpdate> _scheduledUpdates = new();

        public int NoteIndex { get; protected set; }

        public double CurrentTime { get; protected set; }
        public double LastUpdateTime { get; protected set; }

        public double LastQueuedInputTime { get; protected set; }

        public uint CurrentTick { get; protected set; }
        public uint LastTick { get; protected set; }
        public uint FirstWhammyTick { get; protected set; }

        public int CurrentSoloIndex { get; protected set; }
        public int CurrentStarIndex { get; protected set; }
        public int CurrentWaitCountdownIndex { get; protected set; }

        public bool IsSoloActive { get; protected set; }

        public bool IsWaitCountdownActive { get; protected set; }
        public bool IsStarPowerInputActive { get; protected set; }

        protected EngineTimer StarPowerWhammyTimer;

        /// <summary>
        /// A Star Power Sustain was active in the last update.
        /// </summary>
        protected bool WasSpSustainActive;

        /// <summary>
        /// The current Star Power position, in measure ticks.
        /// </summary>
        public uint StarPowerTickPosition { get; protected set; }

        /// <summary>
        /// The previous Star Power position, in measure ticks.
        /// </summary>
        public uint PreviousStarPowerTickPosition { get; protected set; }

        public uint StarPowerTickActivationPosition { get; protected set; }
        public uint StarPowerTickEndPosition { get; protected set; }

        public double StarPowerActivationTime { get; protected set; }
        public double StarPowerEndTime { get; protected set; }

        public double BaseTimeInStarPower { get; protected set; }

        public readonly struct EngineFrameUpdate
        {
            public EngineFrameUpdate(double time, string reason)
            {
                Time = time;
                Reason = reason;
            }

            public readonly double Time;
            public readonly string Reason;
        }

        /// <summary>
        /// Whether or not the specified engine should treat a note as a chord, or separately.
        /// For example, guitars would treat each note as a chord, where as drums would treat them
        /// as singular pieces.
        /// </summary>
        protected readonly bool TreatChordAsSeparate;

        protected bool ReRunHitLogic;

        protected readonly bool IsBot;

        protected BaseEngine(SyncTrack syncTrack, bool isChordSeparate, bool isBot)
        {
            SyncTrack = syncTrack;

            TicksPerQuarterSpBar = SyncTrack.MeasureResolution * 2;
            TicksPerHalfSpBar = TicksPerQuarterSpBar * 2;
            TicksPerFullSpBar = TicksPerQuarterSpBar * 4;

            TreatChordAsSeparate = isChordSeparate;
            IsBot = isBot;
        }

        public EngineTimer GetStarPowerWhammyTimer() => StarPowerWhammyTimer;

        /// <summary>
        /// Gets the number of notes the engine recognizes in a specific note parent.
        /// This number is determined by <see cref="TreatChordAsSeparate"/>.
        /// </summary>
        public int GetNumberOfNotes<T>(T type) where T : Note<T>
        {
            return TreatChordAsSeparate ? type.ChildNotes.Count + 1 : 1;
        }

        protected uint GetCurrentTick(double time)
        {
            return SyncTrack.TimeToTick(time);
        }

        public void Update(double time)
        {
            YargLogger.LogFormatTrace("---- Starting update loop with time {0} ----", time);

            if (!IsBot)
            {
                ProcessInputs(time);
            }

            // Update to the given time
            if (InputQueue.Count > 0)
            {
                YargLogger.LogWarning("Input queue was not fully cleared!");
            }

            YargLogger.LogFormatTrace("Running frame update at {0}", time);
            RunQueuedUpdates(time);
            RunEngineLoop(time);
        }

        private void ProcessInputs(double time)
        {
            while (InputQueue.TryPeek(out var input))
            {
                // Stop here if the inputs are in the future
                if (input.Time > time)
                {
                    YargLogger.LogFormatWarning(
                        "Queued input is in the future! Time being updated to: {0}, input time: {1}", time, input.Time);
                    break;
                }

                // Dequeue this here so inputs that don't meet the above condition aren't completely skipped
                InputQueue.Dequeue();

                // Skip inputs that are in the past
                if (input.Time < CurrentTime)
                {
                    YargLogger.FailFormat(
                        "Queued input is in the past! Current time: {0}, input time: {1}", CurrentTime,
                        input.Time);
                    continue;
                }

                YargLogger.LogFormatTrace("Processing input {0} ({1}) update at {2}", input.GetAction<GuitarAction>(),
                    input.Button, input.Time);
                RunQueuedUpdates(input.Time);

                // Update engine state with input.
                MutateStateWithInput(input);

                // Run the engine.
                RunEngineLoop(input.Time);

                // Skip non-input update if possible
                if (input.Time == time)
                {
                    if (InputQueue.Count > 0)
                    {
                        YargLogger.LogWarning(
                            "Input queue was not fully cleared! Remaining inputs are possibly in the future");
                    }

                    return;
                }
            }
        }

        private void RunQueuedUpdates(double time)
        {
            // 'for' is used here to prevent enumeration exceptions,
            // the list of scheduled updates will be modified by the updates we're running

            GenerateQueuedUpdates(time);
            _scheduledUpdates.Sort((x, y) => x.Time.CompareTo(y.Time));

            if (_scheduledUpdates.Count > 0)
            {
                YargLogger.LogFormatTrace("{0} updates ready to be simulated", _scheduledUpdates.Count);
            }

            while (_scheduledUpdates.Count > 0)
            {
                double updateTime = _scheduledUpdates[0].Time;

                // Skip updates that are in the past
                if (updateTime < CurrentTime)
                {
                    YargLogger.FailFormat(
                        "Scheduled update is in the past! Current time: {0}, update time: {1}", CurrentTime,
                        updateTime);

                    _scheduledUpdates.RemoveAt(0);
                }

                // There should be no scheduled updates for times beyond the one we want to update to
                if (updateTime >= time)
                {
                    YargLogger.FailFormat("Update time is >= than the given time! Update time: {0} ({1}), given time: {2}",
                        updateTime, _scheduledUpdates[0].Reason, time);
                    break;
                }

                YargLogger.LogFormatTrace("Running scheduled update at {0} ({1})", updateTime,
                    item2: _scheduledUpdates[0].Reason);
                RunEngineLoop(updateTime);

                _scheduledUpdates.RemoveAt(0);

                // Generate updates up to the next existing update.
                // This is done to handle any updates that need to be queued if something changes.
                // (For example: a sustain starting then ending within the range of already existing updates)
                if (_scheduledUpdates.Count > 0)
                {
                    GenerateQueuedUpdates(_scheduledUpdates[0].Time);
                }
                else
                {
                    GenerateQueuedUpdates(time);
                }

                _scheduledUpdates.Sort((x, y) => x.Time.CompareTo(y.Time));
            }
        }

        protected abstract void UpdateBot(double time);

        protected virtual void GenerateQueuedUpdates(double nextTime)
        {
            YargLogger.LogFormatTrace("Generating queued updates up to {0}", nextTime);
            var previousTime = CurrentTime;


        }

        protected abstract void UpdateTimeVariables(double time);

        /// <summary>
        /// Queue an input to be processed by the engine.
        /// </summary>
        /// <param name="input">The input to queue into the engine.</param>
        public void QueueInput(ref GameInput input)
        {
            // If the game attempts to queue an input that goes backwards in time, the engine
            // can't handle it and it will cause inconsistencies! In these rare cases, the
            // engine will be forced to move these times forwards a *tiny* bit to prevent
            // issues.

            // In the case that the queue is not in order...
            if (input.Time < LastQueuedInputTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Previous queued input: {0}, input being queued: {1}",
                    LastQueuedInputTime, input.Time);

                input = new GameInput(LastQueuedInputTime, input.Action, input.Integer);
            }

            // In the case that the input is before the current time...
            if (input.Time < CurrentTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Current time: {0}, input being queued: {1}",
                    CurrentTime, input.Time);

                input = new GameInput(CurrentTime, input.Action, input.Integer);
            }

            InputQueue.Enqueue(input);
            LastQueuedInputTime = input.Time;
        }

        public void QueueUpdateTime(double time, string reason)
        {
            // Ignore updates for the current time
            if (time == CurrentTime)
            {
                return;
            }

            // Disallow updates in the past
            if (time < CurrentTime)
            {
                YargLogger.FailFormat(
                    "Cannot queue update in the past! Current time: {0}, time being queued: {1}", CurrentTime,
                    time);
                return;
            }

            // Ignore duplicate updates
            foreach (var update in _scheduledUpdates)
            {
                if (update.Time == time)
                {
                    return;
                }
            }

            _scheduledUpdates.Add(new EngineFrameUpdate(time, reason));
        }

        private void RunEngineLoop(double time)
        {
            do
            {
                ReRunHitLogic = false;
                UpdateTimeVariables(time);

                UpdateStarPower();

                UpdateHitLogic(time);
            } while (ReRunHitLogic);
        }

        public virtual void Reset(bool keepCurrentButtons = false)
        {
            NoteIndex = 0;

            CurrentTime = double.MinValue;
            LastUpdateTime = double.MinValue;

            LastQueuedInputTime = double.MinValue;

            CurrentTick = 0;
            LastTick = 0;

            CurrentSoloIndex = 0;
            CurrentStarIndex = 0;
            CurrentWaitCountdownIndex = 0;

            IsSoloActive = false;

            IsWaitCountdownActive = false;
            IsStarPowerInputActive = false;
        }

        protected abstract void MutateStateWithInput(GameInput gameInput);

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract void UpdateHitLogic(double time);

        protected virtual void UpdateMultiplier()
        {
            BaseStats.ScoreMultiplier = Math.Min((BaseStats.Combo / 10) + 1, BaseParameters.MaxMultiplier);

            if (BaseStats.IsStarPowerActive)
            {
                BaseStats.ScoreMultiplier *= 2;
            }

            RebaseSustains(CurrentTick);
        }

        public double GetStarPowerBarAmount()
        {
            return BaseStats.StarPowerTickAmount / (double) TicksPerFullSpBar;
        }

        protected void ActivateStarPower()
        {
            if (BaseStats.IsStarPowerActive)
            {
                return;
            }

            StarPowerActivationTime = CurrentTime;
            StarPowerTickActivationPosition = StarPowerTickPosition;

            UpdateStarPowerEnds();

            BaseStats.StarPowerActivationCount++;

            YargLogger.LogFormatTrace("Activated at SP tick {0}, ends at SP tick {1}. Start time: {2}, End time: {3}",
                StarPowerTickActivationPosition, StarPowerTickEndPosition, StarPowerActivationTime, StarPowerEndTime);

            BaseStats.IsStarPowerActive = true;

            UpdateMultiplier();

            OnStarPowerStatus?.Invoke(true);
        }

        protected void ReleaseStarPower()
        {
            YargLogger.LogFormatTrace("Star Power ended at {0} (tick: {1})", CurrentTime,
                StarPowerTickPosition);

            BaseStats.IsStarPowerActive = false;

            double spTimeDelta = CurrentTime - StarPowerActivationTime;
            BaseStats.TimeInStarPower = spTimeDelta + BaseTimeInStarPower;

            BaseTimeInStarPower = BaseStats.TimeInStarPower;

            UpdateMultiplier();

            OnStarPowerStatus?.Invoke(false);
        }

        protected void GainStarPower(uint ticks)
        {
            var prevTicks = BaseStats.StarPowerTickAmount;
            BaseStats.StarPowerTickAmount += ticks;

            // Limit amount of ticks to a full bar.
            if (BaseStats.StarPowerTickAmount > TicksPerFullSpBar)
            {
                BaseStats.StarPowerTickAmount = TicksPerFullSpBar;
            }

            // Add the amount of ticks gained to the total ticks gained
            BaseStats.TotalStarPowerTicks += ticks;
            YargLogger.LogFormatTrace("Earned {0} ticks of SP at SP tick position {1}, current: {2}, new total: {3}", BaseStats.StarPowerTickAmount - prevTicks,
                StarPowerTickPosition, BaseStats.StarPowerTickAmount, BaseStats.TotalStarPowerTicks);
            BaseStats.TotalStarPowerBarsFilled = (double) BaseStats.TotalStarPowerTicks / TicksPerFullSpBar;

            if (BaseStats.IsStarPowerActive)
            {
                UpdateStarPowerEnds();
                YargLogger.LogFormatTrace("New end tick and time: {0}, {1}", StarPowerTickEndPosition, StarPowerEndTime);
            }
        }

        protected void UpdateStarPowerEnds()
        {
            uint lastEndTick = StarPowerTickEndPosition;
            double lastEndTime = StarPowerEndTime;

            StarPowerTickEndPosition = StarPowerTickPosition + BaseStats.StarPowerTickAmount;
            StarPowerEndTime = SyncTrack.MeasureTickToTime(StarPowerTickEndPosition);

            YargLogger.LogFormatTrace(
                "Updated Star Power end from {0} ({1}) to {2} ({3})",
                lastEndTime, lastEndTick, StarPowerEndTime, StarPowerTickEndPosition
            );
        }

        protected abstract void UpdateStarPower();

        protected abstract void RebaseSustains(uint baseTick);

        public abstract void AllowStarPower(bool isAllowed);

        /// <summary>
        /// Resets the engine's state back to default and then processes the list of inputs up to the given time.
        /// </summary>
        /// <param name="time">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        /// <returns>The input index that was processed up to.</returns>
        public int ProcessUpToTime(double time, IEnumerable<GameInput> inputs)
        {
            Reset();

            var inputIndex = 0;
            foreach (var input in inputs)
            {
                if (input.Time > time)
                {
                    break;
                }

                InputQueue.Enqueue(input);
                inputIndex++;
            }

            Update(time);

            return inputIndex;
        }

        public abstract (double FrontEnd, double BackEnd) CalculateHitWindow();

        public virtual void SetSpeed(double speed)
        {
            BaseParameters.SongSpeed = speed;
            BaseParameters.HitWindow.Scale = speed;

            StarPowerWhammyTimer.SetSpeed(speed);
        }

        protected static void StartTimer(ref EngineTimer timer, double startTime, double offset = 0)
        {
            if (offset > 0)
            {
                timer.StartWithOffset(startTime, offset);
            }
            else
            {
                timer.Start(startTime);
            }
        }

        protected static bool IsTimeBetween(double time, double prevTime, double nextTime)
        {
            return time > prevTime && time < nextTime;
        }

        protected void IncrementCombo()
        {
            BaseStats.Combo++;
            BaseStats.MaxCombo = Math.Max(BaseStats.MaxCombo, BaseStats.Combo);
            OnComboIncrement?.Invoke(BaseStats.BandComboUnits);
        }

        protected void ResetCombo()
        {
            BaseStats.Combo = 0;
            OnComboReset?.Invoke();
        }

        public void AwardUnisonBonus()
        {
            GainStarPower(TicksPerQuarterSpBar);
            OnUnisonBonusAwarded?.Invoke();
        }
    }
}
