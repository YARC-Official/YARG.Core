using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;

namespace YARG.Core.Engine
{
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

        protected          int[]  StarScoreThresholds { get; }
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
                foreach(var note in Notes)
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

        protected void UpdateTimeVariables(double time)
        {
            if (time < State.CurrentTime)
            {
                YargTrace.Fail($"Time cannot go backwards! Current time: {State.CurrentTime}, new time: {time}");
            }

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
            while (State.NextTimeSigIndex < timeSigs.Count && timeSigs[State.NextTimeSigIndex].Tick <= State.CurrentTick)
            {
                State.CurrentTimeSigIndex++;
                State.NextTimeSigIndex++;
            }

            var currentTimeSig = timeSigs[State.CurrentTimeSigIndex];

            YargTrace.Assert(currentTimeSig.Numerator != 0,
                "Time signature numerator is 0! Ticks per beat/measure will be 0 after this");
            YargTrace.Assert(currentTimeSig.Denominator != 0,
                "Time signature denominator is 0! Ticks per beat/measure will be 0 after this");

            // Set ticks per beat/measure if they haven't been set yet
            if (State.TicksEveryBeat == 0)
            {
                State.TicksEveryBeat = currentTimeSig.GetTicksPerBeat(SyncTrack);
                YargTrace.Assert(State.TicksEveryBeat != 0,
                    "Ticks per beat is 0! Star Power will be NaN after this");
            }
            if (State.TicksEveryMeasure == 0)
            {
                State.TicksEveryMeasure = currentTimeSig.GetTicksPerMeasure(SyncTrack);
                YargTrace.Assert(State.TicksEveryMeasure != 0,
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
                YargTrace.Assert(State.TicksEveryBeat != 0, "Ticks per beat is 0! Star Power will be NaN after this");
                YargTrace.Assert(State.TicksEveryMeasure != 0, "Ticks per measure is 0! Star Power will be NaN after this");
            }

            uint nextTimeSigTick;
            if (State.NextTimeSigIndex < timeSigs.Count)
            {
                nextTimeSigTick = timeSigs[State.NextTimeSigIndex].Tick;
            }
            else
            {
                nextTimeSigTick = uint.MaxValue;
            }

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
                YargTrace.Assert(State.TicksEveryMeasure != 0, "Ticks per measure is 0! Star Power will be NaN after this");
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
                YargTrace.Assert(State.TicksEveryBeat != 0, "Ticks per beat is 0! Star Power will be NaN after this");
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
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

        protected virtual void HitNote(TNoteType note)
        {
            AdvanceToNextNote(note);
        }

        protected virtual void MissNote(TNoteType note)
        {
            AdvanceToNextNote(note);
        }

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
            if (baseTick < State.StarPowerBaseTick)
                YargTrace.Fail($"Star Power base tick cannot go backwards! Went from {State.StarPowerBaseTick} to {baseTick}");

            EngineStats.StarPowerBaseAmount = EngineStats.StarPowerAmount;
            State.StarPowerBaseTick = baseTick;
        }

        protected double CalculateBeatProgress(uint tick, uint baseTick, double factor)
        {
            if (tick < baseTick)
                YargTrace.Fail($"Beat progress cannot go backwards! Base tick: {baseTick}, target tick: {tick}");

            return (tick - baseTick) / (double) State.TicksEveryBeat * factor;
        }

        protected double CalculateMeasureProgress(uint tick, uint baseTick, double factor)
        {
            if (tick < baseTick)
                YargTrace.Fail($"Measure progress cannot go backwards! Base tick: {baseTick}, target tick: {tick}");

            return (tick - baseTick) / (double) State.TicksEveryMeasure * factor;
        }

        protected double CalculateStarPowerBeatProgress(uint tick, uint baseTick)
            => CalculateBeatProgress(tick, baseTick, STAR_POWER_BEAT_AMOUNT);

        protected double CalculateStarPowerMeasureProgress(uint tick, uint baseTick)
            => CalculateMeasureProgress(tick, baseTick, STAR_POWER_MEASURE_AMOUNT);

        protected virtual double CalculateStarPowerGain(uint tick) => 0;

        protected virtual double CalculateStarPowerDrain(uint tick)
            => EngineStats.IsStarPowerActive ? CalculateStarPowerMeasureProgress(tick, State.StarPowerBaseTick) : 0;

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
                YargTrace.LogWarning($"Excessive star power amount {newAmount}! Base: {EngineStats.StarPowerBaseAmount}, gain: {gain}, drain: {drain}");

            EngineStats.StarPowerAmount = Math.Clamp(newAmount, 0, 1);

            YargTrace.Assert(!double.IsNaN(gain), "SP gain is NaN!");
            YargTrace.Assert(!double.IsNaN(drain), "SP drain is NaN!");
            YargTrace.Assert(!double.IsNaN(EngineStats.StarPowerBaseAmount), "SP base is NaN!");
            YargTrace.Assert(!double.IsNaN(EngineStats.StarPowerAmount), "SP amount is NaN!");

            if (tick > State.LastTick)
            {
                double delta = Math.Abs(EngineStats.StarPowerAmount - previous);
                double beatDelta = CalculateStarPowerBeatProgress(tick, State.LastTick);
                double measureDelta = CalculateStarPowerMeasureProgress(tick, State.LastTick);
                double jumpThreshold = Math.Max(beatDelta, measureDelta) * 2;
                if (delta > jumpThreshold)
                    YargTrace.Fail($"Unexpected jump in SP amount! Went from {previous} to {EngineStats.StarPowerAmount}");
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

            if (EngineStats.StarPowerAmount - previous < 0)
                YargTrace.Fail($"Unexpected jump in SP amount after awarding! Went from {previous} to {EngineStats.StarPowerAmount}, should not be decreasing");
            if (Math.Abs(EngineStats.StarPowerAmount - expected) >= 0.001)
                YargTrace.Fail($"Unexpected jump in SP amount after awarding! Went from {previous} to {EngineStats.StarPowerAmount}, should be {expected}");

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
            => IsNoteInWindow(note, out _);

        protected bool IsNoteInWindow(TNoteType note, out bool missed)
        {
            missed = false;

            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            double frontend = EngineParameters.HitWindow.GetFrontEnd(hitWindow);
            double backend = EngineParameters.HitWindow.GetBackEnd(hitWindow);

            if (note.Time + frontend > State.CurrentTime)
            {
                return false;
            }

            if (note.Time + backend <= State.CurrentTime)
            {
                missed = true;
                return false;
            }

            return true;
        }

        private void AdvanceToNextNote(TNoteType note)
        {
            if (note.NextNote is null)
            {
                return;
            }

            note = note.NextNote;

            double dist = GetAverageNoteDistance(note);
            double fullWindow = EngineParameters.HitWindow.CalculateHitWindow(dist);

            double frontEnd = EngineParameters.HitWindow.GetFrontEnd(fullWindow);
            double backEnd = EngineParameters.HitWindow.GetBackEnd(fullWindow);

            // Add consistency anchors for the first note

            // This is the time when the note will enter the hit window in the front end. Engine will update at this time
            double frontEndTime = note.Time + frontEnd;

            // Only queue if the note is not already in the hit window, happens if
            // multiple notes are in the hit window and the back-most one gets hit/missed
            if (frontEndTime > State.CurrentTime)
            {
                QueueUpdateTime(frontEndTime);
            }

            // This is the time when the note will leave the hit window in the back end (miss)
            double backEndTime = note.Time + backEnd;

            // Only queue if note has not already been missed
            // Very rare case; only happens when lagging enough to make a note skip the hit window entirely
            if (backEndTime > State.CurrentTime)
            {
                QueueUpdateTime(backEndTime);
            }

            // Add consistency anchors for the following notes in case of a note skip

            var currentNote = note.NextNote;
            while (currentNote is not null)
            {
                double currentFrontEndTime = currentNote.Time + frontEnd;

                // If the distance between the first note's back-end and this note's front-end
                // is more than the full hit window time, then that means that this method will
                // be called for the missing of the first note, meaning a consistency anchor
                // will be added for this note then.
                if (Math.Abs(currentFrontEndTime - backEndTime) > fullWindow)
                {
                    break;
                }

                if (currentFrontEndTime > State.CurrentTime)
                {
                    QueueUpdateTime(currentFrontEndTime);
                }

                // We don't need to worry about the back end as the first note is guaranteed
                // to exit before it, meaning new anchors will be added.

                // Try to do the next note
                currentNote = currentNote.NextNote;
            }

            State.NoteIndex++;
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