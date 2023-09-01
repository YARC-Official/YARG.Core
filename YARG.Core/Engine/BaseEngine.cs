using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine
{
    // This is a hack lol
    public abstract class BaseEngine
    {
        public bool IsInputQueued => InputQueue.Count > 0;

        public int BaseScore { get; protected set; }

        protected bool IsInputUpdate { get; private set; }
        protected bool IsBotUpdate   { get; private set; }

        protected abstract float[] StarMultiplierThresholds { get; }
        protected abstract float[] StarScoreThresholds      { get; }

        protected readonly SyncTrack SyncTrack;

        protected readonly Queue<GameInput> InputQueue;

        protected readonly uint Resolution;
        protected readonly uint TicksPerSustainPoint;

        protected GameInput CurrentInput;

        protected List<SoloSection> Solos;

        protected BaseEngine(SyncTrack syncTrack)
        {
            SyncTrack = syncTrack;
            Resolution = syncTrack.Resolution;

            TicksPerSustainPoint = Resolution / 25;

            InputQueue = new Queue<GameInput>();
            CurrentInput = new GameInput(-9999, -9999, -9999);
        }

        /// <summary>
        /// Queue an input to be processed by the engine.
        /// </summary>
        /// <param name="input">The input to queue into the engine.</param>
        public void QueueInput(GameInput input)
        {
            InputQueue.Enqueue(input);
        }

        /// <summary>
        /// Updates the engine and processes all inputs currently queued.
        /// </summary>
        public void UpdateEngine()
        {
            if (!IsInputQueued)
            {
                return;
            }

            IsInputUpdate = true;
            ProcessInputs();
        }

        /// <summary>
        /// Updates the engine with no input processing.
        /// </summary>
        /// <param name="time">The time to simulate hit logic at.</param>
        public void UpdateEngine(double time)
        {
            IsInputUpdate = false;
            bool noteUpdated;
            do
            {
                noteUpdated = UpdateHitLogic(time);
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
                // Execute a non-input update using the input 's time.
                // This will update the engine to the time of the first input, missing notes before the input is processed
                UpdateEngine(input.Time);

                CurrentInput = input;
                IsInputUpdate = true;
                bool noteUpdated;
                do
                {
                    noteUpdated = UpdateHitLogic(input.Time);
                    IsInputUpdate = false;
                } while (noteUpdated);
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

        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract bool UpdateHitLogic(double time);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ResetTimer(ref double timer)
        {
            timer = double.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool IsTimerActive(double currentTime, double startTime, double timeThreshold)
        {
            return currentTime - startTime < timeThreshold && currentTime - startTime >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool HasTimerExpired(double currentTime, double startTime, double timeThreshold)
        {
            return currentTime - startTime >= timeThreshold;
        }
    }

    public abstract class BaseEngine<TNoteType, TActionType, TEngineParams, TEngineStats, TEngineState> : BaseEngine
        where TNoteType : Note<TNoteType>
        where TActionType : unmanaged, Enum
        where TEngineParams : BaseEngineParameters
        where TEngineStats : BaseStats, new()
        where TEngineState : BaseEngineState, new()
    {
        protected const int POINTS_PER_NOTE = 50;
        protected const int POINTS_PER_BEAT = 25;

        protected const double STAR_POWER_PHRASE_AMOUNT = 0.25;

        public delegate void NoteHitEvent(int noteIndex, TNoteType note);

        public delegate void NoteMissedEvent(int noteIndex, TNoteType note);

        public delegate void StarPowerPhraseHitEvent(TNoteType note);

        public delegate void StarPowerPhraseMissEvent(TNoteType note);

        public delegate void StarPowerStatusEvent(bool active);

        public delegate void SoloStartEvent(SoloSection soloSection);

        public delegate void SoloEndEvent(SoloSection soloSection);

        public NoteHitEvent    OnNoteHit;
        public NoteMissedEvent OnNoteMissed;

        public StarPowerPhraseHitEvent  OnStarPowerPhraseHit;
        public StarPowerPhraseMissEvent OnStarPowerPhraseMissed;
        public StarPowerStatusEvent     OnStarPowerStatus;

        public SoloStartEvent OnSoloStart;
        public SoloEndEvent OnSoloEnd;

        public readonly TEngineStats EngineStats;

        protected readonly InstrumentDifficulty<TNoteType> Chart;

        protected readonly List<TNoteType> Notes;
        protected readonly TEngineParams   EngineParameters;

        public TEngineState State;

        protected BaseEngine(InstrumentDifficulty<TNoteType> chart, SyncTrack syncTrack,
            TEngineParams engineParameters) : base(syncTrack)
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

        protected void UpdateTimeVariables(double time)
        {
            State.LastUpdateTime = State.CurrentTime;
            State.CurrentTime = time;

            State.LastTick = State.CurrentTick;
            State.CurrentTick = GetCurrentTick(time);

            var timeSigs = SyncTrack.TimeSignatures;
            while (State.NextTimeSigIndex < timeSigs.Count && timeSigs[State.NextTimeSigIndex].Time < time)
            {
                State.CurrentTimeSigIndex++;
                State.NextTimeSigIndex++;
            }

            var currentTimeSig = timeSigs[State.CurrentTimeSigIndex];

            State.TicksEveryEightMeasures = (uint)(Resolution * ((double)4 / currentTimeSig.Denominator) * currentTimeSig.Numerator * 8);
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            CurrentInput = new GameInput(-9999, -9999, -9999);
            InputQueue.Clear();

            State.Reset();
            EngineStats.Reset();

            foreach (var note in Notes)
            {
                note.ResetNoteState();
            }

            foreach (var solo in Solos)
            {
                solo.NotesHit = 0;
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

        protected virtual void StripStarPower(TNoteType note)
        {
            if (!note.IsStarPower)
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

        protected void AwardStarPower(TNoteType note)
        {
            EngineStats.StarPowerAmount += STAR_POWER_PHRASE_AMOUNT;
            if (EngineStats.StarPowerAmount >= 1)
            {
                EngineStats.StarPowerAmount = 1;
            }

            OnStarPowerPhraseHit?.Invoke(note);
        }

        protected void DepleteStarPower(double amount)
        {
            if (!EngineStats.IsStarPowerActive)
            {
                return;
            }

            EngineStats.StarPowerAmount -= amount;
            if (EngineStats.StarPowerAmount <= 0)
            {
                EngineStats.StarPowerAmount = 0;
                EngineStats.IsStarPowerActive = false;
                OnStarPowerStatus?.Invoke(false);
            }
        }

        protected void ActivateStarPower()
        {
            if (EngineStats.IsStarPowerActive)
            {
                return;
            }

            EngineStats.IsStarPowerActive = true;
            OnStarPowerStatus?.Invoke(true);
        }

        protected double GetUsedStarPower()
        {
            return (State.CurrentTick - State.LastTick) / (double)State.TicksEveryEightMeasures;
        }

        protected void StartSolo()
        {
            if(State.CurrentSoloIndex >= Solos.Count)
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

            State.IsSoloActive = false;
            OnSoloEnd?.Invoke(Solos[State.CurrentSoloIndex]);
            State.CurrentSoloIndex++;
        }

        /// <summary>
        /// Resets the engine's state back to default and then processes the list of inputs up to the given time.
        /// </summary>
        /// <param name="time">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        /// <returns>The input index that was processed up to.</returns>
        public virtual int ProcessUpToTime(double time, IList<GameInput> inputs)
        {
            State.Reset();

            foreach (var note in Notes)
            {
                note.ResetNoteState();
            }

            var inputIndex = 0;
            while (inputIndex < inputs.Count && inputs[inputIndex].Time <= time)
            {
                InputQueue.Enqueue(inputs[inputIndex]);
                inputIndex++;
            }

            ProcessInputs();

            return inputIndex;
        }

        /// <summary>
        /// Processes the list of inputs from the given start time to the given end time. Does not reset the engine's state.
        /// </summary>
        /// <param name="startTime">Time to begin processing from.</param>
        /// <param name="endTime">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        public virtual void ProcessFromTimeToTime(double startTime, double endTime, IList<GameInput> inputs)
        {
            throw new NotImplementedException();
        }

        protected abstract int CalculateBaseScore();

        protected bool IsNoteInWindow(TNoteType note)
        {
            return note.Time - State.CurrentTime < EngineParameters.BackEnd &&
                note.Time - State.CurrentTime > EngineParameters.FrontEnd;
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
                var soloNoteCount = 1;
                for (int j = i + 1; j < Notes.Count; j++)
                {
                    soloNoteCount++;
                    var end = Notes[j];
                    if (!end.IsSoloEnd)
                    {
                        continue;
                    }

                    soloSections.Add(new SoloSection(soloNoteCount));

                    // Move i to the end of the solo section
                    i = j + 1;
                    break;
                }
            }

            return soloSections;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetTimer(ref double timer, double maxTime, double negation = 0)
        {
            double diff = Math.Abs(maxTime - negation);
            timer = State.CurrentTime - diff;
        }
    }
}