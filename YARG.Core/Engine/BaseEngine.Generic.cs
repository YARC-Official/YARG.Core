using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;
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

        public delegate void CountdownChangeEvent(uint measuresLeft);

        public NoteHitEvent?    OnNoteHit;
        public NoteMissedEvent? OnNoteMissed;

        public StarPowerPhraseHitEvent?  OnStarPowerPhraseHit;
        public StarPowerPhraseMissEvent? OnStarPowerPhraseMissed;
        public StarPowerStatusEvent?     OnStarPowerStatus;

        public SoloStartEvent? OnSoloStart;
        public SoloEndEvent?   OnSoloEnd;

        public CountdownChangeEvent? OnCountdownChange;

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

            WaitCountdowns = GetWaitCountdowns();
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = State.CurrentTime;

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

            // Only check for WaitCountdowns in this chart if there are any remaining
            if (WaitCountdowns.Count > State.CurrentWaitCountdownIndex)
            {
                if (!State.IsWaitCountdownActive && nextTime > WaitCountdowns[0].Time)
                {
                    State.CurrentWaitCountdownIndex = WaitCountdowns.GetIndexOfPrevious(nextTime);

                    // A new countdown will start before nextTime
                    State.IsWaitCountdownActive = IsTimeBetween(WaitCountdowns[State.CurrentWaitCountdownIndex].Time, previousTime, nextTime);
                }

                if (State.IsWaitCountdownActive)
                {
                    var currentCountdown = WaitCountdowns[State.CurrentWaitCountdownIndex];
                    
                    double queueTime;
                    if (previousTime < currentCountdown.Time)
                    {
                        queueTime = currentCountdown.Time;
                    }
                    else
                    {
                        // Queue updates every frame for the progress bar animation
                        queueTime = currentCountdown.GetNextUpdateTime();
                    }

                    if (IsTimeBetween(queueTime, previousTime, nextTime))
                    {
                        YargLogger.LogFormatTrace("Queuing countdown update time at {0}", queueTime);
                        QueueUpdateTime(queueTime, "Update Countdown");
                    }
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

            while (NextSyncIndex < SyncTrackChanges.Count && State.CurrentTick >= SyncTrackChanges[NextSyncIndex].Tick)
            {
                CurrentSyncIndex++;
            }

            if (State.IsWaitCountdownActive)
            {
                var activeCountdown = WaitCountdowns[State.CurrentWaitCountdownIndex];

                uint countdownMeasuresRemaining = activeCountdown.GetRemainingMeasures(State.CurrentTick);

                UpdateCountdown(countdownMeasuresRemaining);

                State.IsWaitCountdownActive = countdownMeasuresRemaining > WaitCountdown.END_COUNTDOWN_MEASURE;
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
            int multiplierScore = score * EngineStats.ScoreMultiplier;
            EngineStats.CommittedScore += multiplierScore;

            if (EngineStats.IsStarPowerActive)
            {
                // Amount of points just from Star Power is half of the current multiplier (8x total -> 4x SP points)
                EngineStats.StarPowerScore += multiplierScore / 2;
            }
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

        protected virtual uint CalculateStarPowerGain(uint tick) => tick - State.LastTick;

        protected void AwardStarPower(TNoteType note)
        {
            GainStarPower(TicksPerQuarterSpBar);

            OnStarPowerPhraseHit?.Invoke(note);
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

        protected void UpdateCountdown(uint measuresRemaining)
        {
            if (!State.IsWaitCountdownActive)
            {
                return;
            }

            OnCountdownChange?.Invoke(measuresRemaining);
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

        private List<WaitCountdown> GetWaitCountdowns()
        {
            var timeSigs = SyncTrack.TimeSignatures;
            var tempos = SyncTrack.Tempos;
            
            var waitCountdowns = new List<WaitCountdown>();
            for (int i = 0; i < Notes.Count; i++)
            {
                // Compare the note at the current index against the previous note
                // Create a countdown if the distance between the notes is > 10s
                Note<TNoteType> noteOne;

                uint noteOneTickEnd = 0;
                double noteOneTimeEnd = 0;

                if (i > 0) {
                    noteOne = Notes[i-1];
                    noteOneTickEnd = noteOne.TickEnd;
                    noteOneTimeEnd = noteOne.TimeEnd;
                }

                Note<TNoteType> noteTwo = Notes[i];
                double noteTwoTime = noteTwo.Time;

                if (noteTwoTime - noteOneTimeEnd >= WaitCountdown.MIN_SECONDS)
                {
                    // Determine the total number of measures that will pass during this countdown
                    // Find the TimeSignatureChange that contains the start of this Countdown
                    uint countdownTotalMeasures = 0;

                    int currentTimeSigIndex;
                    if (noteOneTickEnd > 0)
                    {
                        currentTimeSigIndex = timeSigs.GetIndexOfPrevious(noteOneTickEnd);
                    }
                    else
                    {
                        currentTimeSigIndex = 0;
                    }

                    // Store an object for each TimeSignatureChange that will occur during this countdown
                    // along with how many measures will pass during each time signature
                    int endTimeSigIndex = timeSigs.GetIndexOfPrevious(noteTwo.Tick);
                    var measuresByTimeSignature = new List<CountdownTimeSig>();

                    var currentSig = timeSigs[currentTimeSigIndex];
                    
                    // Countdown should start at the first measure that begins after Note One ends, unless Note One ends directly on a measure line
                    uint firstEmptyMeasure = (uint) Math.Ceiling( (noteOneTickEnd - currentSig.Tick) / (float) currentSig.GetTicksPerMeasure(SyncTrack) );

                    uint firstCountdownTickThisSig = currentSig.Tick + firstEmptyMeasure * currentSig.GetTicksPerMeasure(SyncTrack);
                    double firstCountdownSecondThisSig = SyncTrack.TickToTime(firstCountdownTickThisSig);
                    
                    // Use the current TempoChange at this TimeSignatureChange to calculate SecondsPerMeasure
                    var currentTempo = tempos.GetPrevious(firstCountdownSecondThisSig);
                    if (currentTempo == null) currentTempo = tempos.Last();
                    
                    while (currentTimeSigIndex < endTimeSigIndex)
                    {
                        // The next TimeSignatureChange will occur before the end of this countdown
                        // Store the total number of measures that will pass until the next time signature takes over
                        var prevSig = timeSigs[currentTimeSigIndex];
                        currentSig = timeSigs[++currentTimeSigIndex];

                        uint prevSigTotalMeasures = WaitCountdown.NormalizeMeasures(prevSig.GetMeasureCount(currentSig.Tick, SyncTrack), prevSig);

                        if (prevSigTotalMeasures > 0)
                        {
                            countdownTotalMeasures += prevSigTotalMeasures;

                            var prevSigTempo = tempos.GetPrevious(currentSig.Tick - 1);
                            if (prevSigTempo == null) prevSigTempo = tempos.Last();

                            uint ticksPerMeasure = WaitCountdown.GetPerNormalizedMeasure(prevSig.GetTicksPerMeasure(SyncTrack), prevSig);
                            double secondsPerMeasure = WaitCountdown.GetPerNormalizedMeasure(prevSig.GetSecondsPerMeasure(prevSigTempo), prevSig);

                            measuresByTimeSignature.Add(new CountdownTimeSig(firstCountdownTickThisSig, firstCountdownSecondThisSig, prevSigTotalMeasures, ticksPerMeasure, secondsPerMeasure));
                        }
                
                        firstCountdownTickThisSig = currentSig.Tick;
                        firstCountdownSecondThisSig = currentSig.Time;

                        currentTempo = tempos.GetPrevious(firstCountdownSecondThisSig);
                        if (currentTempo == null) currentTempo = tempos.Last();
                    }

                    // currentSig now reflects the final TimeSignatureChange that will occur during this countdown
                    // Round the final countdown tick down to the nearest whole measure
                    uint finalSigTotalMeasures = currentSig.GetMeasureCount(noteTwo.Tick, SyncTrack) - currentSig.GetMeasureCount(firstCountdownTickThisSig, SyncTrack);
                    finalSigTotalMeasures = WaitCountdown.NormalizeMeasures(finalSigTotalMeasures, currentSig);
                    countdownTotalMeasures += finalSigTotalMeasures;
                    
                    // Prevent showing countdowns < 4 measures at low BPMs
                    if (countdownTotalMeasures >= WaitCountdown.MIN_MEASURES)
                    {
                        if (finalSigTotalMeasures > 0)
                        {
                            uint ticksPerMeasure = WaitCountdown.GetPerNormalizedMeasure(currentSig.GetTicksPerMeasure(SyncTrack), currentSig);
                            double secondsPerMeasure = WaitCountdown.GetPerNormalizedMeasure(currentSig.GetSecondsPerMeasure(currentTempo), currentSig);

                            measuresByTimeSignature.Add(new CountdownTimeSig(firstCountdownTickThisSig, firstCountdownSecondThisSig, finalSigTotalMeasures, ticksPerMeasure, secondsPerMeasure));
                        }

                        // Create a WaitCountdown instance to reference at runtime
                        var newCountdown = new WaitCountdown(measuresByTimeSignature, countdownTotalMeasures);
                        waitCountdowns.Add(newCountdown);
                        YargLogger.LogFormatTrace(this.GetType().Name+" created a WaitCountdown at time {0} of {1} measures and {2} seconds in length across {3} time signatures",
                                                 newCountdown.Time, countdownTotalMeasures, newCountdown.TimeLength, measuresByTimeSignature.Count);
                    }
                    else
                    {
                        YargLogger.LogFormatTrace(this.GetType().Name+" did not create a WaitCountdown at time {0} of {1} seconds in length because it was only {2} measures long",
                                                 noteOneTimeEnd, noteTwoTime - noteOneTimeEnd, countdownTotalMeasures);                
                    }
                }
            }

            return waitCountdowns;
        }
    }
}