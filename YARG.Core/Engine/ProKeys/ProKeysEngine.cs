using System;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys
{
    public abstract class ProKeysEngine : BaseEngine<ProKeysNote, ProKeysEngineParameters,
        ProKeysStats, ProKeysEngineState>
    {
        public delegate void KeyStateChangeEvent(int key, bool isPressed);
        public delegate void OverhitEvent(int key);

        public KeyStateChangeEvent? OnKeyStateChange;

        public OverhitEvent? OnOverhit;

        protected ProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, true, isBot)
        {
            State.Initialize(engineParameters);
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = State.CurrentTime;

            if (State.ChordStaggerTimer.IsActive)
            {
                if (IsTimeBetween(State.ChordStaggerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing chord stagger end time at {0}", State.ChordStaggerTimer.EndTime);
                    QueueUpdateTime(State.ChordStaggerTimer.EndTime, "Chord Stagger End");
                }
            }

            if (State.FatFingerTimer.IsActive)
            {
                if (IsTimeBetween(State.FatFingerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing fat finger end time at {0}", State.FatFingerTimer.EndTime);
                    QueueUpdateTime(State.FatFingerTimer.EndTime, "Fat Finger End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            // Never retain keys held in Pro Keys because otherwise you get infinite front end
            State.KeyMask = 0;
            // Don't clear this value otherwise the bot won't let go of keys. Should probably be handled better.
            // State.KeyHeldMask = 0;
            State.KeyHit = null;
            State.KeyReleased = null;

            base.Reset(keepCurrentButtons);
        }

        protected virtual void Overhit(int key)
        {
            // Can't overstrum before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (State.NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Cancel overstrum if WaitCountdown is active
            if (State.IsWaitCountdownActive)
            {
                YargLogger.LogFormatTrace("Overstrum prevented during WaitCountdown at time: {0}, tick: {1}", State.CurrentTime, State.CurrentTick);
                return;
            }

            YargLogger.LogFormatTrace("Overhit at {0}", State.CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), State.CurrentTime);
                i--;

                double finalScore = CalculateSustainPoints(ref sustain, State.CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, State.CurrentTime, sustain.HasFinishedScoring);
            }

            if (State.NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[State.NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[State.NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke(key);
        }

        protected override bool CanSustainHold(ProKeysNote note)
        {
            return (State.KeyMask & note.DisjointMask) != 0;
        }

        protected override void HitNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetHitState(true, false);

            ToggleKey(note.Key, false);

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (State.IsSoloActive)
            {
                Solos[State.CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            // Chords only count as one note hit
            if (note.ParentOrSelf.WasFullyHit())
            {
                State.ChordStaggerTimer.Disable();

                EngineStats.Combo++;

                if (EngineStats.Combo > EngineStats.MaxCombo)
                {
                    EngineStats.MaxCombo = EngineStats.Combo;
                }
            }

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            if (note.IsSustain)
            {
                StartSustain(note);
            }

            OnNoteHit?.Invoke(State.NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetMissState(true, false);

            ToggleKey(note.Key, false);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            EngineStats.Combo = 0;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            base.HitNote(note);
        }

        protected override void AddScore(ProKeysNote note)
        {
            AddScore(POINTS_PER_PRO_NOTE);
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_PRO_NOTE * (1 + note.ChildNotes.Count);

                foreach (var child in note.AllNotes)
                {
                    score += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
            }

            return score;
        }

        protected void ToggleKey(int key, bool active)
        {
            State.KeyMask = active ? State.KeyMask | (1 << key) : State.KeyMask & ~(1 << key);
        }
    }
}