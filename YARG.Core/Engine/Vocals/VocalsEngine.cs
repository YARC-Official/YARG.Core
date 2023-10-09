using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals
{
    public abstract class VocalsEngine :
        BaseEngine<VocalNote, VocalsAction, VocalsEngineParameters, VocalsStats, VocalsEngineState>
    {
        public delegate void TargetNoteChangeEvent(VocalNote targetNote);

        public TargetNoteChangeEvent? OnTargetNoteChanged;

        public override bool TreatChordAsSeparate => false;

        protected VocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack,
            VocalsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
            BaseScore = CalculateBaseScore();
        }

        protected override bool HitNote(VocalNote note)
        {
            note.SetHitState(true, false);

            if (note.IsStarPower && note.IsStarPowerEnd)
            {
                AwardStarPower(note);
                EngineStats.PhrasesHit++;
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

            AddScore(note);

            UpdateMultiplier();

            OnNoteHit?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;

            return true;
        }

        protected override void MissNote(VocalNote note)
        {
            note.SetMissState(true, false);

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
            EngineStats.NotesMissed++;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);

            State.NoteIndex++;
        }

        protected override bool CheckForNoteHit()
        {
            var phrase = Notes[State.NoteIndex];

            // Not hittable if the phrase is after the current tick
            if (State.CurrentTick < phrase.Tick) return false;

            // Find the note within the phrase
            VocalNote? note = null;
            foreach (var phraseNote in phrase.ChildNotes)
            {
                // If in bounds, this is the note!
                if (State.CurrentTick >= phraseNote.Tick &&
                    State.CurrentTick <= phraseNote.TotalTickEnd)
                {
                    note = phraseNote;
                    break;
                }
            }

            // No note found to hit
            if (note == null) return false;

            OnTargetNoteChanged?.Invoke(note);

            // return CanNoteBeHit(note);
            return true;
        }

        /// <returns>
        /// Gets the amount of vocal ticks in the phrase.
        /// </returns>
        protected double GetVocalTicksInPhrase(VocalNote phrase)
        {
            double totalTime = 0;
            foreach (var phraseNote in phrase.ChildNotes)
            {
                totalTime += phraseNote.TotalTimeLength;
            }

            return totalTime * EngineParameters.ApproximateVocalFps;
        }

        protected override void AddScore(VocalNote note)
        {
            EngineStats.Score += 1000 * EngineStats.ScoreMultiplier;
            UpdateStars();
        }

        protected override void UpdateMultiplier()
        {
            EngineStats.ScoreMultiplier = EngineStats.Combo switch
            {
                >= 4 => 4,
                _    => EngineStats.Combo + 1
            };

            if (EngineStats.IsStarPowerActive)
            {
                EngineStats.ScoreMultiplier *= 2;
            }
        }

        protected sealed override int CalculateBaseScore()
        {
            return Notes.Where(note => note.ChildNotes.Count > 0).Sum(_ => 1000);
        }
    }
}