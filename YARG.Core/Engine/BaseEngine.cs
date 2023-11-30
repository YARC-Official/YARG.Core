using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;

namespace YARG.Core.Engine
{
    // This is a hack lol
    public abstract class BaseEngine
    {
        public bool IsInputQueued => InputQueue.Count > 0;

        public int BaseScore { get; protected set; }

        public int[] StarScoreThresholds { get; protected set; }

        public EngineEventLogger EventLogger { get; }

        public abstract BaseEngineState BaseState { get; }

        protected bool IsInputUpdate { get; private set; }
        protected bool IsBotUpdate   { get; private set; }

        protected readonly SyncTrack SyncTrack;

        protected readonly Queue<GameInput> InputQueue;

        protected readonly uint Resolution;
        protected readonly uint TicksPerSustainPoint;

        protected GameInput CurrentInput;

        protected List<SoloSection> Solos = new();

        /// <summary>
        /// Whether or not the specified engine should treat a note as a chord, or separately.
        /// For example, guitars would treat each note as a chord, where as drums would treat them
        /// as singular pieces.
        /// </summary>
        public abstract bool TreatChordAsSeparate { get; }

        private double _lastQueuedInputTime = double.MinValue;

        protected BaseEngine(BaseEngineParameters parameters, SyncTrack syncTrack)
        {
            SyncTrack = syncTrack;
            Resolution = syncTrack.Resolution;

            TicksPerSustainPoint = Resolution / 25;

            float[] multiplierThresholds = parameters.StarMultiplierThresholds;
            StarScoreThresholds = new int[multiplierThresholds.Length];
            for (int i = 0; i < multiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = (int) (BaseScore * multiplierThresholds[i]);
            }

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
        public void QueueInput(GameInput input)
        {
            if (input.Time < BaseState.LastQueuedInputTime)
                YargTrace.Fail(
                    $"Input time cannot go backwards! Previous queued input: {BaseState.LastQueuedInputTime}, input being queued: {input.Time}");

            if (input.Time < BaseState.CurrentTime)
                YargTrace.Fail(
                    $"Input time cannot go backwards! Current time: {BaseState.CurrentTime}, input being queued: {input.Time}");

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
        protected const int STAR_POWER_MAX_MEASURES = 8;
        protected const double STAR_POWER_MEASURE_AMOUNT = 1.0 / STAR_POWER_MAX_MEASURES;

        // Max number of beats that it takes to fill SP when gaining
        // SP gain from whammying is done based on beats
        protected const int STAR_POWER_MAX_BEATS = (STAR_POWER_MAX_MEASURES * 4) - 2; // - 2 for leniency
        protected const double STAR_POWER_BEAT_AMOUNT = 1.0 / STAR_POWER_MAX_BEATS;

        // Number of measures that SP phrases will grant when hit
        protected const int STAR_POWER_PHRASE_MEASURE_COUNT = 2;
        protected const double STAR_POWER_PHRASE_AMOUNT = STAR_POWER_PHRASE_MEASURE_COUNT * STAR_POWER_MEASURE_AMOUNT;

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

        public readonly TEngineStats EngineStats;

        protected readonly InstrumentDifficulty<TNoteType> Chart;

        protected readonly List<TNoteType> Notes;
        protected readonly TEngineParams   EngineParameters;

        public TEngineState State;

        public override BaseEngineState BaseState => State;

        protected BaseEngine(InstrumentDifficulty<TNoteType> chart, SyncTrack syncTrack,
            TEngineParams engineParameters) : base(engineParameters, syncTrack)
        {
            Chart = chart;
            Notes = Chart.Notes;

            EngineParameters = engineParameters;

            EngineStats = new TEngineStats();
            State = new TEngineState();
            State.Reset();

            EngineStats.ScoreMultiplier = 1;

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
            if (time < State.CurrentTime)
                YargTrace.Fail($"Time cannot go backwards! Current time: {State.CurrentTime}, new time: {time}");

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
            while (State.NextTimeSigIndex < timeSigs.Count && timeSigs[State.NextTimeSigIndex].Time < time)
            {
                State.CurrentTimeSigIndex++;
                State.NextTimeSigIndex++;
            }

            var currentTimeSig = timeSigs[State.CurrentTimeSigIndex];

            // Set ticks per beat/measure if they haven't been set yet
            if (State.TicksEveryBeat == 0)
                State.TicksEveryBeat = currentTimeSig.GetTicksPerBeat(SyncTrack);
            if (State.TicksEveryMeasure == 0)
                State.TicksEveryMeasure = currentTimeSig.GetTicksPerMeasure(SyncTrack);

            // Rebase SP on time signature change
            if (previousTimeSigIndex != State.CurrentTimeSigIndex)
            {
                // Update SP drain to ensure the base is accurate, e.g. if a time signature change happens
                // after 4 measures of SP drainage, the base should be exactly 0.5
                UpdateStarPowerAmount(currentTimeSig.Tick);
                RebaseStarPower(currentTimeSig.Tick);
                // Update ticks per beat/measure *after* rebasing, otherwise SP won't update correctly
                State.TicksEveryBeat = currentTimeSig.GetTicksPerBeat(SyncTrack);
                State.TicksEveryMeasure = currentTimeSig.GetTicksPerMeasure(SyncTrack);
            }

            uint nextTimeSigTick;
            if (State.NextTimeSigIndex < timeSigs.Count)
                nextTimeSigTick = timeSigs[State.NextTimeSigIndex].Tick;
            else
                nextTimeSigTick = uint.MaxValue;

            // Detect misaligned time signatures
            uint measureCount = currentTimeSig.GetMeasureProgress(State.CurrentTick, SyncTrack).count;
            uint currentMeasureTick = currentTimeSig.Tick + (State.TicksEveryMeasure * measureCount);
            if ((currentMeasureTick + State.TicksEveryMeasure) > nextTimeSigTick &&
                // Only do this once for the misaligned TS, not every update
                State.TicksEveryMeasure != (nextTimeSigTick - currentMeasureTick))
            {
                // Rebase again on misaligned time signatures
                if (currentMeasureTick != currentTimeSig.Tick)
                {
                    UpdateStarPowerAmount(currentMeasureTick);
                    RebaseStarPower(currentMeasureTick);
                }
                State.TicksEveryMeasure = nextTimeSigTick - currentMeasureTick;
            }

            // Handle the last beat of misaligned time signatures correctly
            uint beatCount = currentTimeSig.GetBeatProgress(State.CurrentTick, SyncTrack).count;
            uint currentBeatTick = currentTimeSig.Tick + (State.TicksEveryBeat * beatCount);
            if ((currentBeatTick + State.TicksEveryBeat) > nextTimeSigTick &&
                // Only do this once for the misaligned TS, not every update
                State.TicksEveryBeat != (nextTimeSigTick - currentBeatTick))
            {
                // Rebase again on misaligned time signatures
                UpdateStarPowerAmount(currentBeatTick);
                RebaseStarPower(currentBeatTick);
                State.TicksEveryBeat = nextTimeSigTick - currentBeatTick;
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

        protected abstract void UpdateMultiplier();

        protected void UpdateStars()
        {
            if (State.CurrentStarIndex >= StarScoreThresholds.Length || StarScoreThresholds[0] == 0)
            {
                return;
            }

            if (EngineStats.Score >= StarScoreThresholds[State.CurrentStarIndex])
            {
                EngineStats.Stars++;
                State.CurrentStarIndex++;
            }
        }

        protected virtual void StripStarPower(TNoteType? note)
        {
            if (note is null || !note.IsStarPower)
            {
                return;
            }

            EngineStats.PhrasesMissed++;

            // Strip star power from the note and all its children
            note.Flags &= ~NoteFlags.StarPower;
            foreach (var childNote in note.ChildNotes)
            {
                childNote.Flags &= ~NoteFlags.StarPower;
            }

            // Look back until finding the start of the phrase
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

            // Do this to warn of a null reference if its used below
            prevNote = null;

            // Look forward until finding the end of the phrase
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

            OnStarPowerPhraseMissed?.Invoke(note);
        }

        protected virtual void RebaseStarPower(uint baseTick)
        {
            if (baseTick < State.StarPowerBaseTick)
                YargTrace.Fail($"Star Power base tick cannot go backwards! Went from {State.StarPowerBaseTick} to {baseTick}");

            EngineStats.StarPowerBaseAmount = EngineStats.StarPowerAmount;
            State.StarPowerBaseTick = baseTick;
        }

        protected double CalculateStarPowerBeatProgress(uint tick, uint baseTick)
        {
            return (tick - baseTick) / (double) State.TicksEveryBeat * STAR_POWER_BEAT_AMOUNT;
        }

        protected double CalculateStarPowerMeasureProgress(uint tick, uint baseTick)
        {
            return (tick - baseTick) / (double) State.TicksEveryMeasure * STAR_POWER_MEASURE_AMOUNT;
        }

        protected virtual double CalculateStarPowerGain(uint tick) => 0;

        protected virtual double CalculateStarPowerDrain(uint tick)
            => EngineStats.IsStarPowerActive ? CalculateStarPowerMeasureProgress(tick, State.StarPowerBaseTick) : 0;

        protected void UpdateStarPowerAmount(uint tick)
        {
            double gain = CalculateStarPowerGain(tick);
            double drain = CalculateStarPowerDrain(tick);
            EngineStats.StarPowerAmount = Math.Clamp(EngineStats.StarPowerBaseAmount + gain - drain, 0, 1);
        }

        protected void AwardStarPower(TNoteType note)
        {
            EngineStats.StarPowerAmount += STAR_POWER_PHRASE_AMOUNT;
            if (EngineStats.StarPowerAmount > 1)
            {
                EngineStats.StarPowerAmount = 1;
            }

            RebaseStarPower(State.CurrentTick);
            OnStarPowerPhraseHit?.Invoke(note);
        }

        protected void UpdateStarPower()
        {
            UpdateStarPowerAmount(State.CurrentTick);

            if (EngineStats.IsStarPowerActive && EngineStats.StarPowerAmount <= 0)
            {
                EventLogger.LogEvent(new StarPowerEngineEvent(State.CurrentTime)
                {
                    IsActive = false,
                });

                EngineStats.StarPowerAmount = 0;
                EngineStats.IsStarPowerActive = false;
                RebaseStarPower(State.CurrentTick);

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

            RebaseStarPower(State.CurrentTick);
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

                // Old engine says this is 200 *, but I'm not sure that's right?? Isn't it 2x the note's worth, not 4x?
                double points = 100 * currentSolo.NotesHit * multiplier;

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

        protected abstract int CalculateBaseScore();

        protected bool IsNoteInWindow(TNoteType note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));

            return note.Time - State.CurrentTime < EngineParameters.HitWindow.GetBackEnd(hitWindow) &&
                note.Time - State.CurrentTime > EngineParameters.HitWindow.GetFrontEnd(hitWindow);
        }

        public double GetAverageNoteDistance(TNoteType note)
        {
            double currentToNext;
            double previousToCurrent = currentToNext = EngineParameters.HitWindow.MaxWindow / 2;

            if (note.PreviousNote is not null)
            {
                previousToCurrent = (note.Time - note.PreviousNote.Time) / 2;
            }

            if (note.NextNote is not null)
            {
                currentToNext = (note.NextNote.Time - note.Time) / 2;
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