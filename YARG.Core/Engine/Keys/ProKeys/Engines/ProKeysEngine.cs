using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Keys
{
    public abstract class ProKeysEngine : KeysEngine<ProKeysNote>
    {
        protected const int POINTS_PER_PRO_KEYS_NOTE = 120;

        protected EngineTimer FatFingerTimer;
        protected ProKeysNote? FatFingerNote;
        protected int? FatFingerKey;

        protected bool IsGlissandoActive = false;

        protected override double[] KeyPressTimes { get; } = new double[(int) ProKeysAction.Key25 + 1];

        public EngineTimer GetFatFingerTimer() => FatFingerTimer;

        protected ProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            KeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
            KeyPressTimes = new double[(int)ProKeysAction.Key25 + 1];
            FatFingerTimer = new("Fat Finger", engineParameters.FatFingerWindow);

            for (int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }

            GetWaitCountdowns(Notes);
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);

            if (FatFingerTimer.IsActive)
            {
                var previousTime = CurrentTime;

                if (IsTimeBetween(FatFingerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing fat finger end time at {0}", FatFingerTimer.EndTime);
                    QueueUpdateTime(FatFingerTimer.EndTime, "Fat Finger End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            base.Reset(keepCurrentButtons);

            FatFingerKey = null;
            FatFingerTimer.Reset();
            FatFingerNote = null;
        }

        protected override bool CanSustainHold(ProKeysNote note)
        {
            return (KeyMask & note.DisjointMask) != 0;
        }

        protected override void HitNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            bool partiallyHit = false;
            foreach(var child in note.ParentOrSelf.AllNotes)
            {
                if (child.WasHit || child.WasMissed)
                {
                    partiallyHit = true;
                    break;
                }
            }

            note.SetHitState(true, false);

            KeyPressTimes[note.Key] = DEFAULT_PRESS_TIME;

            // Cancel rest of hit logic during BRE phrase. Key on CodaHasStarted, not IsCodaActive
            // (see DrumsEngine.HitNote): an end-tick finale is judged after the coda's EndTime,
            // where IsCodaActive is already false.
            if (CodaHasStarted && note.IsBigRockEnding)
            {
                // Be sure to disable the stagger timer so it doesn't run long
                ChordStaggerTimer.Disable(CurrentTime, early: true);

                base.HitNote(note);
                return;
            }

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower)
            {
                if (EngineStats.IsStarPowerActive && EngineParameters.NoStarPowerOverlap)
                {
                    StripStarPower(note);
                }
                else if (note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
                {
                    AwardStarPower(note);
                }
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (IsSoloActive)
            {
                Solos[CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (note.ParentOrSelf.WasFullyHit())
            {
                ChordStaggerTimer.Disable(CurrentTime, early: true);
            }

            // Only increase combo for the first note in a chord
            if (!partiallyHit)
            {
                IncrementCombo();
            }

            EngineStats.IncrementNotesHit(note, CurrentTime);

            UpdateMultiplier();

            AddScore(note);

            if (note.IsSustain)
            {
                StartSustain(note);
            }

            if (note.IsGlissando)
            {
                UpdateLaneAutohitExpireTime();

                if (note.IsGlissandoStart)
                {
                    IsGlissandoActive = true;
                }

                if (note.IsGlissandoEnd)
                {
                    IsGlissandoActive = false;
                }
            }

            OnNoteHit?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            KeyPressTimes[note.Key] = DEFAULT_PRESS_TIME;

            // Can't miss a note during the coda. Key on CodaHasStarted, not IsCodaActive (see
            // DrumsEngine.MissNote): the miss is judged at the back-end time, which for an
            // end-tick finale falls after the coda's EndTime where IsCodaActive is already false.
            if (CodaHasStarted && note.IsBigRockEnding)
            {
                // Resolve the whole chord, matching GuitarEngine (see DrumsEngine.MissNote).
                note.SetHitState(true, true);
                base.HitNote(note);
                return;
            }

            // Autohit glissando notes as long as the player keeps providing inputs
            if (note.IsGlissando && note.Time < LaneAutohitExpireTime)
            {
                note.SetHitState(true, false);
                base.HitNote(note);
                return;
            }

            note.SetMissState(true, false);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note is { IsSoloStart: true, IsSoloEnd: true } && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                // While a solo is active, end the current solo and immediately start the next.
                if (IsSoloActive)
                {
                    EndSolo();
                    StartSolo();
                }
                else
                {
                    // If no solo is currently active, start and immediately end the solo.
                    StartSolo();
                    EndSolo();
                }
            }
            else if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }
            else if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (note.IsGlissandoStart)
            {
                IsGlissandoActive = true;
            }

            if (note.IsGlissandoEnd)
            {
                IsGlissandoActive = false;
            }

            // If no notes within a chord were hit, combo is 0
            if (note.ParentOrSelf.WasFullyMissed())
            {
                ResetCombo();
            }
            else
            {
                // If any of the notes in a chord were hit, the combo for that note is rewarded, but it is reset back to 1
                ResetCombo();
                IncrementCombo();
            }

            UpdateMultiplier();

            if (CodaHasStarted)
            {
                Codas[CurrentCodaIndex].MissNote();
            }

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void AddScore(ProKeysNote note)
        {
            int scoredNotePoints = ApplyAccuracyScore(note, POINTS_PER_PRO_KEYS_NOTE);

            AddScore(scoredNotePoints);
            EngineStats.NoteScore += scoredNotePoints;
        }

        protected sealed override (int baseScore, int noteScore) CalculateChartScores()
        {
            double baseScore = 0;
            double noteScore = 0;
            int combo = 0;
            int multiplier;
            foreach (var note in Notes)
            {
                // Exclude BRE notes from base score calculation since they can't be scored
                if (note.IsBigRockEnding)
                {
                    continue;
                }

                // Get the current multiplier given the current combo
                multiplier = Math.Min((combo / 10) + 1, BaseParameters.MaxMultiplier);
                double pointsForNote = POINTS_PER_PRO_KEYS_NOTE * (1 + note.ChildNotes.Count);
                baseScore += multiplier * pointsForNote;
                noteScore += pointsForNote;
                foreach (var child in note.AllNotes)
                {
                    int pointsForSustain = (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                    baseScore += multiplier * pointsForSustain;
                    noteScore += pointsForSustain;
                }

                // Pro Keys combo increments per chord, not per note.
                combo++;
            }

            YargLogger.LogDebug($"[Pro Keys] Base score: {baseScore}, Max Combo: {combo}");
            return ((int) Math.Round(baseScore), (int) Math.Round(noteScore));
        }

        protected override int CalculateMaxScoreWithoutStarPower()
        {
            double maxScore = 0;
            int combo = 0;
            foreach (var note in Notes)
            {
                if (note.IsBigRockEnding)
                {
                    continue;
                }

                combo++;
                int multiplier = GetScoreMultiplierForCombo(combo);
                foreach (var child in note.AllNotes)
                {
                    maxScore += multiplier * POINTS_PER_PRO_KEYS_NOTE;
                    maxScore += multiplier * Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
            }

            return (int) Math.Round(maxScore) + EngineStats.MaxSoloBonusPoints + CalculateTotalCodaBonus();
        }

        protected override bool IsKeyInTime(ProKeysNote note, double frontEnd) => IsKeyInTime(note, note.Key, frontEnd);
    }
}