using System;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys
{
    public abstract class ProKeysEngine : BaseEngine<ProKeysNote, ProKeysEngineParameters,
        ProKeysStats, ProKeysEngineState>
    {
        public Action? OnOverhit;

        protected ProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, true, isBot)
        {
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            var keys = State.KeyMask;

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                State.KeyMask = keys;
            }
        }

        protected virtual void Overhit()
        {
            // Can't overstrum before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (State.NoteIndex >= Chart.Notes.Count /*&& ActiveSustains.Count == 0*/)
            {
                return;
            }

            YargLogger.LogFormatTrace("Overhit at {0}", State.CurrentTime);

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

            OnOverhit?.Invoke();
        }

        protected override void HitNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetHitState(true, true);

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd)
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

            EngineStats.Combo = 0;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            base.MissNote(note);
        }

        protected override void AddScore(ProKeysNote note)
        {
            int notePoints = POINTS_PER_PRO_NOTE * (1 + note.ChildNotes.Count) * EngineStats.ScoreMultiplier;
            AddScore(notePoints);
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_PRO_NOTE * (1 + note.ChildNotes.Count);

                foreach (var child in note.ChordEnumerator())
                {
                    score += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
            }

            return score;
        }
    }
}