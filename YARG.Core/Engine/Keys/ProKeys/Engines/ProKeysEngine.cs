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
                    EngineStats.StarPowerPhrasesHit++;
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

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            if (note.IsSustain)
            {
                StartSustain(note);
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

            note.SetMissState(true, false);

            KeyPressTimes[note.Key] = DEFAULT_PRESS_TIME;

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

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void AddScore(ProKeysNote note)
        {
            AddScore(POINTS_PER_PRO_KEYS_NOTE);
            EngineStats.NoteScore += POINTS_PER_PRO_KEYS_NOTE;
        }

        protected sealed override int CalculateBaseScore()
        {
            double score = 0;
            int combo = 0;
            int multiplier;
            double weight;
            foreach (var note in Notes)
            {
                // Get the current multiplier given the current combo
                multiplier = Math.Min((combo / 10) + 1, BaseParameters.MaxMultiplier);

                // invert it to calculate leniency
                weight = 1.0 * multiplier / BaseParameters.MaxMultiplier;
                score += weight * (POINTS_PER_PRO_KEYS_NOTE * (1 + note.ChildNotes.Count));

                foreach (var child in note.AllNotes)
                {
                    score += weight * (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }

                // Pro Keys combo increments per chord, not per note.
                combo++;
            }

            YargLogger.LogDebug($"[Pro Keys] Base score: {score}, Max Combo: {combo}");
            return (int) Math.Round(score);
        }

        protected override bool IsKeyInTime(ProKeysNote note, double frontEnd) => IsKeyInTime(note, note.Key, frontEnd);
    }
}