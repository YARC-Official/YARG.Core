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
        public bool IsInputQueued => InputQueue.Count > 0;

        public int BaseScore { get; protected set; }

        public EngineEventLogger EventLogger { get; }

        public abstract BaseEngineState      BaseState      { get; }
        public abstract BaseEngineParameters BaseParameters { get; }
        public abstract BaseStats            BaseStats      { get; }

        protected bool StarPowerIsAllowed = true;

        protected bool IsInputUpdate { get; private set; }
        protected bool IsBotUpdate   { get; private set; }

        protected readonly SyncTrack SyncTrack;

        protected readonly Queue<GameInput> InputQueue;

        protected readonly uint Resolution;

        protected GameInput CurrentInput;

        protected List<SoloSection> Solos = new();

        /// <summary>
        /// Whether or not the specified engine should treat a note as a chord, or separately.
        /// For example, guitars would treat each note as a chord, where as drums would treat them
        /// as singular pieces.
        /// </summary>
        protected readonly bool TreatChordAsSeparate;

        protected BaseEngine(SyncTrack syncTrack, bool isChordSeparate)
        {
            SyncTrack = syncTrack;
            Resolution = syncTrack.Resolution;
            TreatChordAsSeparate = isChordSeparate;

            EventLogger = new EngineEventLogger();
            InputQueue = new Queue<GameInput>();
            CurrentInput = new GameInput(-9999, -9999, -9999);
        }

        /// <summary>
        /// Gets the number of notes the engine recognizes in a specific note parent.
        /// This number is determined by <see cref="TreatChordAsSeparate"/>.
        /// </summary>
        public int GetNumberOfNotes<T>(T type) where T : Note<T>
        {
            return TreatChordAsSeparate ? type.ChildNotes.Count + 1 : 1;
        }

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

        /// <summary>
        /// Updates the engine and processes all inputs currently queued.
        /// </summary>
        public void UpdateEngineInputs()
        {
            if (!IsInputQueued)
            {
                return;
            }

            ProcessInputs();
        }

        /// <summary>
        /// Updates the engine with no input processing.
        /// </summary>
        /// <param name="time">The time to simulate hit logic at.</param>
        public void UpdateEngineToTime(double time)
        {
            IsInputUpdate = false;
            UpdateUpToTime(time);
        }

        protected void RunHitLogic(double time)
        {
            bool noteUpdated;
            do
            {
                noteUpdated = UpdateHitLogic(time);
                IsInputUpdate = false;
            } while (noteUpdated);
        }

        /// <summary>
        /// Loops through the input queue and processes each input. Invokes engine logic for each input.
        /// </summary>
        protected void ProcessInputs()
        {
            // Start to process inputs in queue.
            while (InputQueue.TryDequeue(out var input))
            {
                // This will update the engine to the time of the input.
                // However, it does not use the input for the update.
                IsInputUpdate = false;
                UpdateUpToTime(input.Time);

                // Process the input and run hit logic for it.
                CurrentInput = input;
                IsInputUpdate = true;
                RunHitLogic(input.Time);
            }
        }

        protected uint GetCurrentTick(double time)
        {
            return SyncTrack.TimeToTick(time);
        }

        public virtual void UpdateBot(double songTime)
        {
            IsInputUpdate = false;
            IsBotUpdate = true;
        }

        public abstract void AllowStarPower(bool isAllowed);

        public abstract void Reset(bool keepCurrentButtons = false);

        protected abstract void UpdateUpToTime(double time);

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract bool UpdateHitLogic(double time);

        /// <summary>
        /// Resets the engine's state back to default and then processes the list of inputs up to the given time.
        /// </summary>
        /// <param name="time">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        /// <returns>The input index that was processed up to.</returns>
        public abstract int ProcessUpToTime(double time, IEnumerable<GameInput> inputs);

        public abstract (double FrontEnd, double BackEnd) CalculateHitWindow();

        public virtual void SetSpeed(double speed)
        {
            BaseParameters.SongSpeed = speed;
            BaseParameters.HitWindow.Scale = speed;
        }
    }

    public abstract class BaseEngine<TNoteType, TEngineParams, TEngineStats, TEngineState> : BaseEngine
        where TNoteType : Note<TNoteType>
        where TEngineParams : BaseEngineParameters
        where TEngineStats : BaseStats, new()
        where TEngineState : BaseEngineState, new()
    {
        protected const int POINTS_PER_NOTE = 50;
        protected const int POINTS_PER_BEAT = 25;

        // Max number of measures that SP will last when draining
        // SP draining is done based on measures
        protected const int    STAR_POWER_MAX_MEASURES   = 8;
        protected const double STAR_POWER_MEASURE_AMOUNT = 1.0 / STAR_POWER_MAX_MEASURES;

        // Max number of beats that it takes to fill SP when gaining
        // SP gain from whammying is done based on beats
        protected const int    STAR_POWER_MAX_BEATS   = (STAR_POWER_MAX_MEASURES * 4) - 2; // - 2 for leniency
        protected const double STAR_POWER_BEAT_AMOUNT = 1.0 / STAR_POWER_MAX_BEATS;

        // Number of measures that SP phrases will grant when hit
        protected const int    STAR_POWER_PHRASE_MEASURE_COUNT = 2;
        protected const double STAR_POWER_PHRASE_AMOUNT = STAR_POWER_PHRASE_MEASURE_COUNT * STAR_POWER_MEASURE_AMOUNT;

        // Beat fraction to use for the sustain burst threshold
        protected const int SUSTAIN_BURST_FRACTION = 4;

        public delegate void NoteHitEvent(int noteIndex, TNoteType note);

        public delegate void NoteMissedEvent(int noteIndex, TNoteType note);

        public delegate void StarPowerPhraseHitEvent(TNoteType note);

        public delegate void StarPowerPhraseMissEvent(TNoteType note);

        public delegate void StarPowerStatusEvent(bool active);

        public delegate void SoloStartEvent(SoloSection soloSection);

        public delegate void SoloEndEvent(SoloSection soloSection);

        public NoteHitEvent?    OnNoteHit;
        public NoteMissedEvent? OnNoteMissed;

        public StarPowerPhraseHitEvent?  OnStarPowerPhraseHit;
        public StarPowerPhraseMissEvent? OnStarPowerPhraseMissed;
        public StarPowerStatusEvent?     OnStarPowerStatus;

        public SoloStartEvent? OnSoloStart;
        public SoloEndEvent?   OnSoloEnd;

        protected readonly int[]  StarScoreThresholds;
        protected readonly double TicksPerSustainPoint;
        protected readonly uint   SustainBurstThreshold;

        public readonly TEngineStats EngineStats;

        protected readonly InstrumentDifficulty<TNoteType> Chart;

        protected readonly List<TNoteType> Notes;
        protected readonly TEngineParams   EngineParameters;

        public TEngineState State;

        public override BaseEngineState      BaseState      => State;
        public override BaseEngineParameters BaseParameters => EngineParameters;
        public override BaseStats            BaseStats      => EngineStats;

        protected BaseEngine(InstrumentDifficulty<TNoteType> chart, SyncTrack syncTrack,
            TEngineParams engineParameters, bool isChordSeparate) : base(syncTrack, isChordSeparate)
        {
            Chart = chart;
            Notes = Chart.Notes;
            EngineParameters = engineParameters;

            EngineStats = new TEngineStats();
            State = new TEngineState();
            State.Reset();

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

        protected override void UpdateUpToTime(double time)
        {
            var currentTime = State.CurrentTime;

            var noteUpdateIndex = State.NoteIndex;

            // Get the index of the next note to update to
            while (noteUpdateIndex < Notes.Count && currentTime > Notes[noteUpdateIndex].Time)
            {
                noteUpdateIndex++;
            }

            // Update the engine to the next note
            while (noteUpdateIndex < Notes.Count && Notes[noteUpdateIndex].Time < time)
            {
                RunHitLogic(Notes[noteUpdateIndex].Time);

                // Move to the next note
                noteUpdateIndex++;
            }

            // Updated to the last note before the given time
            // Now we update the engine to the given time
            RunHitLogic(time);
        }

        protected void UpdateTimeVariables(double time)
        {
            YargLogger.AssertFormat(time >= State.CurrentTime,
                "Time cannot go backwards! Current time: {0}, new time: {1}", State.CurrentTime, time);

            // Only update the last time if the current time has changed
            if (Math.Abs(time - State.CurrentTime) > double.Epsilon)
            {
                State.LastUpdateTime = State.CurrentTime;
                State.LastTick = State.CurrentTick;
            }

            State.CurrentTime = time;
            State.CurrentTick = GetCurrentTick(time);

            int previousTimeSigIndex = State.CurrentTimeSigIndex;
            var timeSigs = SyncTrack.TimeSignatures;
            while (State.NextTimeSigIndex < timeSigs.Count &&
                timeSigs[State.NextTimeSigIndex].Tick <= State.CurrentTick)
            {
                State.CurrentTimeSigIndex++;
                State.NextTimeSigIndex++;
            }

            var currentTimeSig = timeSigs[State.CurrentTimeSigIndex];

            YargLogger.Assert(currentTimeSig.Numerator != 0,
                "Time signature numerator is 0! Ticks per beat/measure will be 0 after this");
            YargLogger.Assert(currentTimeSig.Denominator != 0,
                "Time signature denominator is 0! Ticks per beat/measure will be 0 after this");

            // Set ticks per beat/measure if they haven't been set yet
            if (State.TicksEveryBeat == 0)
            {
                State.TicksEveryBeat = currentTimeSig.GetTicksPerBeat(SyncTrack);
                YargLogger.Assert(State.TicksEveryBeat != 0,
                    "Ticks per beat is 0! Star Power will be NaN after this");
            }

            if (State.TicksEveryMeasure == 0)
            {
                State.TicksEveryMeasure = currentTimeSig.GetTicksPerMeasure(SyncTrack);
                YargLogger.Assert(State.TicksEveryMeasure != 0,
                    "Ticks per measure is 0! Star Power will be NaN after this");
            }

            // Rebase SP on time signature change
            if (previousTimeSigIndex != State.CurrentTimeSigIndex)
            {
                // Update progresses to ensure values are accurate, e.g. if a time signature change happens
                // after 4 measures of SP drainage, the base should be exactly 0.5
                UpdateProgressValues(currentTimeSig.Tick);
                RebaseProgressValues(currentTimeSig.Tick);
                // Update ticks per beat/measure *after* rebasing, otherwise SP won't update correctly
                State.TicksEveryBeat = currentTimeSig.GetTicksPerBeat(SyncTrack);
                State.TicksEveryMeasure = currentTimeSig.GetTicksPerMeasure(SyncTrack);
                YargLogger.Assert(State.TicksEveryBeat != 0,
                    "Ticks per beat is 0! Star Power will be NaN after this");
                YargLogger.Assert(State.TicksEveryMeasure != 0,
                    "Ticks per measure is 0! Star Power will be NaN after this");
            }

            uint nextTimeSigTick;
            if (State.NextTimeSigIndex < timeSigs.Count)
                nextTimeSigTick = timeSigs[State.NextTimeSigIndex].Tick;
            else
                nextTimeSigTick = uint.MaxValue;

            // Detect misaligned time signatures
            uint measureCount = currentTimeSig.GetMeasureCount(State.CurrentTick, SyncTrack);
            uint currentMeasureTick = currentTimeSig.Tick + (State.TicksEveryMeasure * measureCount);
            if ((currentMeasureTick + State.TicksEveryMeasure) > nextTimeSigTick &&
                // Only do this once for the misaligned TS, not every update
                State.TicksEveryMeasure != (nextTimeSigTick - currentMeasureTick))
            {
                // Rebase again on misaligned time signatures
                if (currentMeasureTick != currentTimeSig.Tick)
                {
                    UpdateProgressValues(currentMeasureTick);
                    RebaseProgressValues(currentMeasureTick);
                }

                State.TicksEveryMeasure = nextTimeSigTick - currentMeasureTick;
                YargLogger.Assert(State.TicksEveryMeasure != 0,
                    "Ticks per measure is 0! Star Power will be NaN after this");
            }

            // Handle the last beat of misaligned time signatures correctly
            uint beatCount = currentTimeSig.GetBeatCount(State.CurrentTick, SyncTrack);
            uint currentBeatTick = currentTimeSig.Tick + (State.TicksEveryBeat * beatCount);
            if ((currentBeatTick + State.TicksEveryBeat) > nextTimeSigTick &&
                // Only do this once for the misaligned TS, not every update
                State.TicksEveryBeat != (nextTimeSigTick - currentBeatTick))
            {
                // Rebase again on misaligned time signatures
                UpdateProgressValues(currentBeatTick);
                RebaseProgressValues(currentBeatTick);
                State.TicksEveryBeat = nextTimeSigTick - currentBeatTick;
                YargLogger.Assert(State.TicksEveryBeat != 0,
                    "Ticks per beat is 0! Star Power will be NaN after this");
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
            CurrentInput = new GameInput(-9999, -9999, -9999);
            InputQueue.Clear();

            State.Reset();
            EngineStats.Reset();

            EventLogger.Clear();

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

        protected abstract bool CheckForNoteHit();

        /// <summary>
        /// Checks if the given note can be hit with the current input state.
        /// </summary>
        /// <param name="note">The Note to attempt to hit.</param>
        /// <returns>True if note can be hit. False otherwise.</returns>
        protected abstract bool CanNoteBeHit(TNoteType note);

        protected abstract bool HitNote(TNoteType note);

        protected abstract void MissNote(TNoteType note);

        protected abstract void AddScore(TNoteType note);

        protected void AddScore(int score)
        {
            EngineStats.CommittedScore += score;
            UpdateStars();
        }

        protected void UpdateStars()
        {
            // Update which star we're on
            while (State.CurrentStarIndex < StarScoreThresholds.Length &&
                EngineStats.StarScore > StarScoreThresholds[State.CurrentStarIndex])
            {
                State.CurrentStarIndex++;
            }

            // Calculate current star progress
            float progress = 0f;
            if (State.CurrentStarIndex < StarScoreThresholds.Length)
            {
                int previousPoints = State.CurrentStarIndex > 0 ? StarScoreThresholds[State.CurrentStarIndex - 1] : 0;
                int nextPoints = StarScoreThresholds[State.CurrentStarIndex];
                progress = YargMath.InverseLerpF(previousPoints, nextPoints, EngineStats.StarScore);
            }

            EngineStats.Stars = State.CurrentStarIndex + progress;
        }

        protected virtual void UpdateMultiplier()
        {
            EngineStats.ScoreMultiplier = Math.Min((EngineStats.Combo / 10) + 1, EngineParameters.MaxMultiplier);

            if (EngineStats.IsStarPowerActive)
            {
                EngineStats.ScoreMultiplier *= 2;
            }
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

        protected virtual void RebaseProgressValues(uint baseTick)
        {
            RebaseStarPower(baseTick);
        }

        protected void RebaseStarPower(uint baseTick)
        {
            YargLogger.AssertFormat(baseTick >= State.StarPowerBaseTick,
                "Star Power base tick cannot go backwards! Went from {0} to {1}", State.StarPowerBaseTick, baseTick);

            EngineStats.StarPowerBaseAmount = EngineStats.StarPowerAmount;
            State.StarPowerBaseTick = baseTick;
        }

        protected double CalculateBeatProgress(uint tick, uint baseTick, double factor)
        {
            YargLogger.AssertFormat(tick >= baseTick,
                "Beat progress cannot go backwards! Base tick: {0}, target tick: {1}", baseTick, tick);

            return (tick - baseTick) / (double) State.TicksEveryBeat * factor;
        }

        protected double CalculateMeasureProgress(uint tick, uint baseTick, double factor)
        {
            YargLogger.AssertFormat(tick >= baseTick,
                "Measure progress cannot go backwards! Base tick: {0}, target tick: {1}", baseTick, tick);

            return (tick - baseTick) / (double) State.TicksEveryMeasure * factor;
        }

        protected double CalculateStarPowerBeatProgress(uint tick, uint baseTick) =>
            CalculateBeatProgress(tick, baseTick, STAR_POWER_BEAT_AMOUNT);

        protected double CalculateStarPowerMeasureProgress(uint tick, uint baseTick) =>
            CalculateMeasureProgress(tick, baseTick, STAR_POWER_MEASURE_AMOUNT);

        protected virtual double CalculateStarPowerGain(uint tick) => 0;

        protected virtual double CalculateStarPowerDrain(uint tick) =>
            EngineStats.IsStarPowerActive ? CalculateStarPowerMeasureProgress(tick, State.StarPowerBaseTick) : 0;

        protected virtual void UpdateProgressValues(uint tick)
        {
            UpdateStarPowerAmount(tick);
        }

        protected void UpdateStarPowerAmount(uint tick)
        {
            double previous = EngineStats.StarPowerAmount;
            double gain = CalculateStarPowerGain(tick);
            double drain = CalculateStarPowerDrain(tick);

            double newAmount = EngineStats.StarPowerBaseAmount + gain - drain;
            if (newAmount > 1.5)
                YargLogger.LogFormatWarning("Excessive star power amount {0}! Base: {1}, gain: {2}, drain: {3}",
                    newAmount, EngineStats.StarPowerBaseAmount, gain, drain);

            EngineStats.StarPowerAmount = Math.Clamp(newAmount, 0, 1);

            YargLogger.Assert(!double.IsNaN(gain), "SP gain is NaN!");
            YargLogger.Assert(!double.IsNaN(drain), "SP drain is NaN!");
            YargLogger.Assert(!double.IsNaN(EngineStats.StarPowerBaseAmount), "SP base is NaN!");
            YargLogger.Assert(!double.IsNaN(EngineStats.StarPowerAmount), "SP amount is NaN!");

            if (tick > State.LastTick)
            {
                double delta = Math.Abs(EngineStats.StarPowerAmount - previous);
                double beatDelta = CalculateStarPowerBeatProgress(tick, State.LastTick);
                double measureDelta = CalculateStarPowerMeasureProgress(tick, State.LastTick);
                double jumpThreshold = Math.Max(beatDelta, measureDelta) * 2;
                YargLogger.AssertFormat(delta <= jumpThreshold,
                    "Unexpected jump in SP amount! Went from {0} to {1}", previous, EngineStats.StarPowerAmount);
            }
        }

        protected void AwardStarPower(TNoteType note)
        {
            double previous = EngineStats.StarPowerAmount;
            double expected = EngineStats.StarPowerAmount += STAR_POWER_PHRASE_AMOUNT;
            if (EngineStats.StarPowerAmount > 1)
            {
                expected = EngineStats.StarPowerAmount = 1;
            }

            RebaseProgressValues(State.CurrentTick);

            YargLogger.AssertFormat(EngineStats.StarPowerAmount - previous >= 0,
                "Unexpected jump in SP amount after awarding! Went from {0} to {1}, should not be decreasing",
                    previous, EngineStats.StarPowerAmount);
            YargLogger.AssertFormat(Math.Abs(EngineStats.StarPowerAmount - expected) < 0.001,
                "Unexpected jump in SP amount after awarding! Went from {0} to {1}, should be {2}",
                    previous, EngineStats.StarPowerAmount, expected);

            OnStarPowerPhraseHit?.Invoke(note);
        }

        protected void UpdateStarPower()
        {
            UpdateProgressValues(State.CurrentTick);

            if (EngineStats.IsStarPowerActive && EngineStats.StarPowerAmount <= 0)
            {
                EventLogger.LogEvent(new StarPowerEngineEvent(State.CurrentTime)
                {
                    IsActive = false,
                });

                EngineStats.StarPowerAmount = 0;
                EngineStats.IsStarPowerActive = false;
                RebaseProgressValues(State.CurrentTick);

                UpdateMultiplier();
                OnStarPowerStatus?.Invoke(false);
            }
        }

        protected void ActivateStarPower()
        {
            if (EngineStats.IsStarPowerActive)
            {
                return;
            }

            EventLogger.LogEvent(new StarPowerEngineEvent(State.CurrentTime)
            {
                IsActive = true,
            });

            RebaseProgressValues(State.CurrentTick);
            EngineStats.IsStarPowerActive = true;

            UpdateMultiplier();
            OnStarPowerStatus?.Invoke(true);
        }

        protected void StartSolo()
        {
            if (State.CurrentSoloIndex >= Solos.Count)
            {
                return;
            }

            State.IsSoloActive = true;
            OnSoloStart?.Invoke(Solos[State.CurrentSoloIndex]);
        }

        protected void EndSolo()
        {
            if (!State.IsSoloActive)
            {
                return;
            }

            var currentSolo = Solos[State.CurrentSoloIndex];

            double soloPercentage = currentSolo.NotesHit / (double) currentSolo.NoteCount;

            if (soloPercentage < 0.6)
            {
                currentSolo.SoloBonus = 0;
            }
            else
            {
                double multiplier = Math.Clamp((soloPercentage - 0.6) / 0.4, 0, 1);

                // In case the solo is over 100% (some bugs can cause this), cap the notes hit to the note count
                int notesHit = Math.Min(currentSolo.NotesHit, currentSolo.NoteCount);

                // Old engine says this is 200 *, but I'm not sure that's right?? Isn't it 2x the note's worth, not 4x?
                double points = 100 * notesHit * multiplier;

                // Round down to nearest 50 (kinda just makes sense I think?)
                points -= points % 50;

                currentSolo.SoloBonus = (int) points;
            }

            EngineStats.SoloBonuses += currentSolo.SoloBonus;

            State.IsSoloActive = false;

            OnSoloEnd?.Invoke(Solos[State.CurrentSoloIndex]);
            State.CurrentSoloIndex++;
        }

        public override int ProcessUpToTime(double time, IEnumerable<GameInput> inputs)
        {
            Reset();

            var inputIndex = 0;
            double lastInputTime = 0;
            foreach (var input in inputs)
            {
                lastInputTime = input.Time;
                if (input.Time > time)
                {
                    break;
                }

                InputQueue.Enqueue(input);
                inputIndex++;
            }

            ProcessInputs();

            if (lastInputTime < time)
            {
                UpdateEngineToTime(time);
            }

            return inputIndex;
        }

        public sealed override (double FrontEnd, double BackEnd) CalculateHitWindow()
        {
            var maxWindow = EngineParameters.HitWindow.MaxWindow;

            if (State.NoteIndex >= Notes.Count)
            {
                return (EngineParameters.HitWindow.GetFrontEnd(maxWindow),
                    EngineParameters.HitWindow.GetBackEnd(maxWindow));
            }

            var noteDistance = GetAverageNoteDistance(Notes[State.NoteIndex]);
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

        protected bool IsNoteInWindow(TNoteType note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            return note.Time - State.CurrentTime < EngineParameters.HitWindow.GetBackEnd(hitWindow) &&
                note.Time - State.CurrentTime > EngineParameters.HitWindow.GetFrontEnd(hitWindow);
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
            for (int i = 0; i < Notes.Count; i++)
            {
                var start = Notes[i];
                if (!start.IsSoloStart)
                {
                    continue;
                }

                // note is a SoloStart

                // Try to find a solo end
                int soloNoteCount = GetNumberOfNotes(start);
                for (int j = i + 1; j < Notes.Count; j++)
                {
                    var end = Notes[j];

                    soloNoteCount += GetNumberOfNotes(end);

                    if (!end.IsSoloEnd) continue;

                    soloSections.Add(new SoloSection(soloNoteCount));

                    // Move i to the end of the solo section
                    i = j;
                    break;
                }
            }

            return soloSections;
        }
    }
}