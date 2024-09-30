using System;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProGuitar
{
    public abstract class ProGuitarEngine : BaseEngine<ProGuitarNote, ProGuitarEngineParameters,
        ProGuitarStats>
    {
        private const int STRING_COUNT = 6;

        // TODO: Make this into an engine param
        protected const int EXTRA_STRING_HIT_LIMIT = 2;

        public delegate void OverstrumEvent();
        public OverstrumEvent? OnOverstrum;

        public FretBytes HeldFrets = FretBytes.CreateEmpty();

        protected byte Strums = 0;
        protected bool HasFretted;
        protected bool HasTapped = true;

        protected int  ExtraStringsHit      = 0;
        protected bool ExtraStringOverstrum = false;

        public bool WasNoteGhosted { get; protected set; }

        /// <summary>
        /// The amount of time a hopo is allowed to take a strum input.
        /// Strum after this time and it will overstrum.
        /// </summary>
        protected EngineTimer HopoLeniencyTimer;

        /// <summary>
        /// The amount of time a strum can be inputted from the time of the first strum.
        /// This allows chords to be strummed properly (as otherwise, the player would
        /// have to hit all strings at once).
        /// </summary>
        protected EngineTimer ChordStrumLeniencyTimer;

        /// <summary>
        /// The amount of time after a note hit where strums can still be inputted to count
        /// towards the previous note.
        /// </summary>
        protected EngineTimer AfterStrumLeniencyTimer;

        public ProGuitarEngine(InstrumentDifficulty<ProGuitarNote> chart, SyncTrack syncTrack,
            ProGuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
            HopoLeniencyTimer = new EngineTimer(engineParameters.HopoLeniency);
            ChordStrumLeniencyTimer = new EngineTimer(engineParameters.ChordStrumLeniency);

            // TODO: Use a separate engine parameter
            AfterStrumLeniencyTimer = new EngineTimer(engineParameters.ChordStrumLeniency);
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = CurrentTime;

            // TODO: Sustains

            // Check all timers
            if (HopoLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(HopoLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing hopo leniency end time at {0}", HopoLeniencyTimer.EndTime);
                    QueueUpdateTime(HopoLeniencyTimer.EndTime, "HOPO Leniency End");
                }
            }

            if (ChordStrumLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(ChordStrumLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing chord strum leniency end time at {0}",
                        ChordStrumLeniencyTimer.EndTime);
                    QueueUpdateTime(ChordStrumLeniencyTimer.EndTime, "Chord Strum Leniency End");
                }
            }

            if (AfterStrumLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(AfterStrumLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing after strum leniency end time at {0}",
                        AfterStrumLeniencyTimer.EndTime);
                    QueueUpdateTime(AfterStrumLeniencyTimer.EndTime, "After Strum Leniency End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            if (!keepCurrentButtons)
            {
                for (int i = 0; i < STRING_COUNT; i++)
                {
                    HeldFrets[i] = 0;
                }
            }

            Strums = 0;
            HasFretted = false;
            HasTapped = true;

            ExtraStringsHit = 0;
            ExtraStringOverstrum = false;

            WasNoteGhosted = false;

            HopoLeniencyTimer.Disable();
            ChordStrumLeniencyTimer.Disable();
            StarPowerWhammyTimer.Disable();

            ActiveSustains.Clear();

            base.Reset(keepCurrentButtons);
        }

        protected virtual void Overstrum()
        {
            // Can't overstrum before first note is hit/missed
            if (NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Cancel overstrum if WaitCountdown is active
            if (IsWaitCountdownActive)
            {
                YargLogger.LogFormatTrace("Overstrum prevented during WaitCountdown at time: {0}, tick: {1}",
                    CurrentTime, CurrentTick);
                return;
            }

            YargLogger.LogFormatTrace("Overstrummed at {0}", CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), CurrentTime);
                i--;

                double finalScore = CalculateSustainPoints(ref sustain, CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, CurrentTime, sustain.HasFinishedScoring);
            }

            if (NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overstrums++;

            UpdateMultiplier();

            OnOverstrum?.Invoke();
        }

        protected override bool CanSustainHold(ProGuitarNote note)
        {
            // TODO: Extended sustain stuff

            return CanNoteBeHit(note);
        }

        protected override void HitNote(ProGuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Fret, NoteIndex, note.WasHit, note.WasMissed);
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

            if (IsSoloActive)
            {
                Solos[CurrentSoloIndex].NotesHit++;
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

            WasNoteGhosted = false;

            OnNoteHit?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(ProGuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Fret, NoteIndex, note.WasHit, note.WasMissed);
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

            WasNoteGhosted = false;

            EngineStats.Combo = 0;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.MissNote(note);
        }

        protected override void AddScore(ProGuitarNote note)
        {
            int notePoints = POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
            EngineStats.NoteScore += notePoints;
            AddScore(notePoints);
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
    }
}