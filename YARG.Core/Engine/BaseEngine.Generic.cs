using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseEngine<TNoteType, TEngineParams, TEngineStats, TEngineState> : BaseEngine
        where TNoteType : Note<TNoteType>
        where TEngineParams : BaseEngineParameters
        where TEngineStats : BaseStats, new()
        where TEngineState : BaseEngineState, new()
    {
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
            TEngineParams engineParameters, bool isChordSeparate, bool isBot)
            : base(syncTrack, isChordSeparate, isBot)
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

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = BaseState.CurrentTime;

            for (int i = State.NoteIndex; i < Notes.Count; i++)
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
                    if(IsTimeBetween(noteFrontEnd, previousTime, nextTime))
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

                if (IsTimeBetween(_spEndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatDebug("Queuing Star Power End Time at {0}", _spEndTime);
                    QueueUpdateTime(_spEndTime, "SP End Time");
                }
            }
        }

        protected override void UpdateTimeVariables(double time)
        {
            if (time < State.CurrentTime)
            {
                YargLogger.FailFormat("Time cannot go backwards! Current time: {0}, new time: {1}", State.CurrentTime,
                    time);
            }

            State.LastUpdateTime = State.CurrentTime;
            State.LastTick = State.CurrentTick;

            State.CurrentTime = time;
            State.CurrentTick = GetCurrentTick(time);

            while (NextSyncIndex < SyncTrackChanges.Count && SyncTrackChanges[NextSyncIndex].Tick <= State.CurrentTick)
            {
                CurrentSyncIndex++;
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

        protected abstract void CheckForNoteHit();

        /// <summary>
        /// Checks if the given note can be hit with the current input state.
        /// </summary>
        /// <param name="note">The Note to attempt to hit.</param>
        /// <returns>True if note can be hit. False otherwise.</returns>
        protected abstract bool CanNoteBeHit(TNoteType note);

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
                YargLogger.LogFormatTrace("Missed note (Index: {0}) ({1}) due to note skip at {2}", State.NoteIndex, prevNote.IsParent ? "Parent" : "Child", State.CurrentTime);
                MissNote(prevNote);

                if (TreatChordAsSeparate)
                {
                    foreach (var child in prevNote.ChildNotes)
                    {
                        YargLogger.LogFormatTrace("Missed note (Index: {0}) ({1}) due to note skip at {2}", State.NoteIndex, child.IsParent ? "Parent" : "Child", State.CurrentTime);
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

        }

        protected virtual uint CalculateStarPowerGain(uint tick) => tick - State.LastTick;

        protected virtual void UpdateProgressValues(uint tick)
        {

        }

        private uint ticksTaken = 0;
        private uint startingAmount = 0;

        protected void DrainStarPower(uint tick)
        {
            uint drain = tick - State.LastTick;
            ticksTaken += drain;

            YargLogger.LogFormatDebug("Drain: {0} (at: {1}). Ticks taken: {2}", drain, tick, ticksTaken);

            if (ticksTaken >= startingAmount)
            {
                YargLogger.LogFormatDebug("Took all ticks at {0}, ending SP", tick);
            }

            int newAmount = (int)EngineStats.StarPowerTickAmount - (int)drain;

            if (newAmount <= 0)
            {
                newAmount = 0;
            }

            EngineStats.StarPowerTickAmount = (uint)newAmount;
        }

        private double _spEndTime;

        protected void ActivateStarPower()
        {
            if (EngineStats.IsStarPowerActive)
            {
                return;
            }

            var endTime = GetStarPowerEndTime(State.CurrentTick, EngineStats.StarPowerTickAmount);
            YargLogger.LogFormatDebug("Star Power activated at {0} ({1}), will end at {2} (tick: {3})", State.CurrentTime, State.CurrentTick, endTime, State.CurrentTick + EngineStats.StarPowerTickAmount);

            startingAmount = EngineStats.StarPowerTickAmount;
            _spEndTime = endTime;

            RebaseProgressValues(State.CurrentTick);
            EngineStats.IsStarPowerActive = true;

            UpdateMultiplier();
            OnStarPowerStatus?.Invoke(true);
        }

        protected void ReleaseStarPower()
        {
            YargLogger.LogFormatDebug("Star Power ended at {0} (tick: {1})", State.CurrentTime, State.CurrentTick);
            EngineStats.StarPowerBarAmount = 0;
            EngineStats.IsStarPowerActive = false;
            RebaseProgressValues(State.CurrentTick);

            UpdateMultiplier();
            OnStarPowerStatus?.Invoke(false);
        }

        protected void AwardStarPower(TNoteType note)
        {
            EngineStats.StarPowerTickAmount += TicksPerQuarterSpBar;

            YargLogger.LogFormatDebug("Ticks: {0}", EngineStats.StarPowerTickAmount);

            RebaseProgressValues(State.CurrentTick);

            OnStarPowerPhraseHit?.Invoke(note);
        }

        protected virtual void UpdateStarPower()
        {
            if (EngineStats.IsStarPowerActive)
            {
                DrainStarPower(State.CurrentTick);

                if(EngineStats.StarPowerTickAmount <= 0)
                {
                    ReleaseStarPower();
                }
            }

            if (State.IsStarPowerInputActive && CanStarPowerActivate)
            {
                ActivateStarPower();
            }
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

        protected bool IsNoteInWindow(TNoteType note) => IsNoteInWindow(note, out _);

        protected bool IsNoteInWindow(TNoteType note, double time) =>
            IsNoteInWindow(note, out _, time);

        protected bool IsNoteInWindow(TNoteType note, out bool missed) =>
            IsNoteInWindow(note, out missed, State.CurrentTime);

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

        private void AdvanceToNextNote(TNoteType note)
        {
            State.NoteIndex++;
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