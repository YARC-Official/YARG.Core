using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
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

        // Max number of beats that it takes to fill SP when gaining
        // SP gain from whammying is done based on beats
        protected const int STAR_POWER_MAX_BEATS = (STAR_POWER_MAX_MEASURES * 4) - 2; // - 2 for leniency

        // Beat fraction to use for the sustain burst threshold
        protected const int SUSTAIN_BURST_FRACTION = 4;

        public bool IsInputQueued => InputQueue.Count > 0;

        public bool CanStarPowerActivate => BaseStats.StarPowerTickAmount >= TicksPerHalfSpBar;

        public int BaseScore { get; protected set; }

        public EngineEventLogger EventLogger { get; } = new();

        public abstract BaseEngineState      BaseState      { get; }
        public abstract BaseEngineParameters BaseParameters { get; }
        public abstract BaseStats            BaseStats      { get; }

        protected readonly SyncTrack SyncTrack;

        protected readonly uint Resolution;

        public readonly uint TicksPerQuarterSpBar;
        public readonly uint TicksPerHalfSpBar;
        public readonly uint TicksPerFullSpBar;

        protected List<SoloSection> Solos = new();

        protected readonly Queue<GameInput> InputQueue = new();

        protected readonly List<SyncTrackChange> SyncTrackChanges = new();

        private readonly List<EngineFrameUpdate> _scheduledUpdates = new();

        private readonly List<SyncTrackChange> _syncTrackChanges = new();

        private readonly List<double> _starPowerTempoTsTicks = new();

        public struct EngineFrameUpdate
        {
            public double Time;
            public string Reason;
        }

        /// <summary>
        /// Whether or not the specified engine should treat a note as a chord, or separately.
        /// For example, guitars would treat each note as a chord, where as drums would treat them
        /// as singular pieces.
        /// </summary>
        protected readonly bool TreatChordAsSeparate;

        protected bool ReRunHitLogic;

        protected readonly bool IsBot;

        protected int CurrentSyncIndex;

        protected int NextSyncIndex => CurrentSyncIndex + 1;

        protected SyncTrackChange CurrentSyncTrackState => SyncTrackChanges[CurrentSyncIndex];

        protected BaseEngine(SyncTrack syncTrack, bool isChordSeparate, bool isBot)
        {
            SyncTrack = syncTrack;
            Resolution = syncTrack.Resolution;

            TicksPerQuarterSpBar = (uint) Math.Round((double) STAR_POWER_MAX_BEATS / 4 * syncTrack.Resolution);
            TicksPerHalfSpBar = TicksPerQuarterSpBar * 2;
            TicksPerFullSpBar = TicksPerQuarterSpBar * 4;

            TreatChordAsSeparate = isChordSeparate;
            IsBot = isBot;

            int tsIndex = 0;
            int changeIndex = 0;
            for (int i = 0; i < syncTrack.Tempos.Count; i++)
            {
                var tempo = syncTrack.Tempos[i];
                var timeSignature = syncTrack.TimeSignatures[tsIndex];

                SyncTrackChanges.Add(new SyncTrackChange(changeIndex, tempo, timeSignature, tempo.Time, tempo.Tick));
                changeIndex++;

                uint nextTempoTick = i + 1 < syncTrack.Tempos.Count ? syncTrack.Tempos[i + 1].Tick : uint.MaxValue;
                for (int nextTsIndex = tsIndex + 1; nextTsIndex < syncTrack.TimeSignatures.Count; nextTsIndex++)
                {
                    var nextTs = syncTrack.TimeSignatures[nextTsIndex];
                    if (nextTs.Tick >= nextTempoTick)
                    {
                        break;
                    }

                    if (nextTs.Tick == tempo.Tick)
                    {
                        SyncTrackChanges[^1].TimeSignature = nextTs;
                    }
                    else
                    {
                        SyncTrackChanges.Add(new SyncTrackChange(changeIndex, tempo, nextTs, nextTs.Time,
                            nextTs.Tick));
                        changeIndex++;
                    }

                    tsIndex = nextTsIndex;
                }
            }

            _starPowerTempoTsTicks.Add(0);
            for (int i = 1; i < SyncTrackChanges.Count; i++)
            {
                var change = SyncTrackChanges[i];
                var prevChange = SyncTrackChanges[i - 1];

                double deltaTime = change.Time - prevChange.Time;

                var tempo = syncTrack.Tempos.GetPrevious(change.Tick - 1);
                var ts = syncTrack.TimeSignatures.GetPrevious(change.Tick - 1);

                // Calculate the number of star power ticks that occur during this tempo
                var starPowerTicks = GetStarPowerDrainPeriodToTicks(deltaTime, tempo!, ts!);
                _starPowerTempoTsTicks.Add(_starPowerTempoTsTicks[^1] + starPowerTicks);
            }

            CurrentSyncIndex = 0;
        }

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
                if (input.Time < BaseState.CurrentTime)
                {
                    YargLogger.FailFormat(
                        "Queued input is in the past! Current time: {0}, input time: {1}", BaseState.CurrentTime, input.Time);
                    continue;
                }

                YargLogger.LogFormatTrace("Processing input {0} ({1}) update at {2}", input.GetAction<GuitarAction>(), input.Button, input.Time);
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
                        YargLogger.LogWarning("Input queue was not fully cleared! Remaining inputs are possibly in the future");
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
            int i = 0;
            for (; i < _scheduledUpdates.Count; i++)
            {
                double updateTime = _scheduledUpdates[i].Time;

                // Skip updates that are in the past
                if (updateTime < BaseState.CurrentTime)
                {
                    YargLogger.FailFormat(
                        "Scheduled update is in the past! Current time: {0}, update time: {1}", BaseState.CurrentTime, updateTime);
                    continue;
                }

                // There should be no scheduled updates for times beyond the one we want to update to
                if (updateTime >= time)
                {
                    YargLogger.FailFormat("Update time is >= than the given time! Update time: {0}, given time: {1}", updateTime, time);
                    continue;
                }

                YargLogger.LogFormatTrace("Running scheduled update at {0} ({1})", updateTime, item2: _scheduledUpdates[i].Reason);
                RunEngineLoop(updateTime);
            }

            // Remove all processed updates
            _scheduledUpdates.RemoveRange(0, i);
        }

        protected abstract void UpdateBot(double time);

        protected virtual void GenerateQueuedUpdates(double nextTime)
        {
            YargLogger.LogFormatTrace("Generating queued updates up to {0}", nextTime);
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
            if (input.Time < BaseState.LastQueuedInputTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Previous queued input: {0}, input being queued: {1}",
                    BaseState.LastQueuedInputTime, input.Time);

                input = new GameInput(BaseState.LastQueuedInputTime, input.Action, input.Integer);
            }

            // In the case that the input is before the current time...
            if (input.Time < BaseState.CurrentTime)
            {
                YargLogger.LogFormatWarning(
                    "Engine was forced to move an input time! Current time: {0}, input being queued: {1}",
                    BaseState.CurrentTime, input.Time);

                input = new GameInput(BaseState.CurrentTime, input.Action, input.Integer);
            }

            InputQueue.Enqueue(input);
            BaseState.LastQueuedInputTime = input.Time;
        }

        public void QueueUpdateTime(double time, string reason)
        {
            // Ignore updates for the current time
            if (time == BaseState.CurrentTime)
            {
                return;
            }

            // Disallow updates in the past
            if (time < BaseState.CurrentTime)
            {
                YargLogger.FailFormat(
                    "Cannot queue update in the past! Current time: {0}, time being queued: {1}", BaseState.CurrentTime, time);
                return;
            }

            // Ignore duplicate updates
            if (_scheduledUpdates.Any(i => i.Time == time))
            {
                return;
            }

            _scheduledUpdates.Add(new EngineFrameUpdate
                { Time = time, Reason = reason });
        }

        private void RunEngineLoop(double time)
        {
            do
            {
                ReRunHitLogic = false;
                UpdateTimeVariables(time);
                UpdateHitLogic(time);
            } while (ReRunHitLogic);
        }

        public abstract void Reset(bool keepCurrentButtons = false);

        protected abstract void MutateStateWithInput(GameInput gameInput);

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract void UpdateHitLogic(double time);

        public double GetStarPowerEndTime(uint startTick, uint starPowerTicks)
        {
            // Basic implementation
            uint endTick = startTick + starPowerTicks;

            return SyncTrack.TickToTime(endTick);
        }

        /// <summary>
        /// Calculates the drain to gain ratio for Star Power for a given <see cref="TimeSignatureChange"/>
        /// </summary>
        /// <param name="timeSignature"></param>
        /// <returns></returns>
        /// <remarks>
        /// The drain factor notes how much longer a game tick lasts during Star Power drain. If there are 192 game ticks
        /// and the drain factor was 1.6, then the Star Power drain would be 192/1.6 = 120 game ticks. This is the number of
        /// Star Power ticks that would be drained during the 192 game ticks.
        /// </remarks>
        private double GetStarPowerDrainFactor(TimeSignatureChange timeSignature)
        {
            var standardDrain = 4.0 / timeSignature.Denominator * timeSignature.Numerator * STAR_POWER_MAX_MEASURES;

            return standardDrain / STAR_POWER_MAX_BEATS;
        }

        /// <summary>
        /// Calculates the number of Star Power ticks that occur during a given period of time.
        /// </summary>
        /// <param name="period">Time period in seconds</param>
        /// <param name="tempo">Tempo to drain at</param>
        /// <param name="timeSignature">Time Signature to drain at</param>
        /// <returns></returns>
        private double GetStarPowerDrainPeriodToTicks(double period, TempoChange tempo, TimeSignatureChange timeSignature)
        {
            var drainFactor = GetStarPowerDrainFactor(timeSignature);

            // Amount of time in between each chart tick.
            var timePerTick = tempo.SecondsPerBeat / Resolution;

            // Amount of time in between each star power tick during star power.
            var timePerStarPowerTick = timePerTick * drainFactor;

            var starPowerTicksInPeriod = period / timePerStarPowerTick;

            return starPowerTicksInPeriod;
        }

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
    }
}