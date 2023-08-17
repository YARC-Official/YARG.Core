using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarAction, GuitarEngineParameters,
        GuitarStats, GuitarEngineState>
    {
        public delegate void OverstrumEvent();

        public delegate void SustainStartEvent(GuitarNote note);

        public delegate void SustainEndEvent(GuitarNote note, double timeEnded);

        public OverstrumEvent    OnOverstrum;
        public SustainStartEvent OnSustainStart;
        public SustainEndEvent   OnSustainEnd;

        protected List<GuitarNote> ActiveSustains;

        protected sealed override float[] StarMultiplierThresholds { get; } =
            { 0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f };

        protected sealed override float[] StarScoreThresholds { get; }

        protected GuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters) : base(chart, syncTrack, engineParameters)
        {
            BaseScore = CalculateBaseScore();
            ActiveSustains = new List<GuitarNote>();

            StarScoreThresholds = new float[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = BaseScore * StarMultiplierThresholds[i];
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            byte buttons = State.ButtonMask;
            ActiveSustains.Clear();

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                State.ButtonMask = buttons;
            }
        }

        protected abstract void UpdateSustains();

        protected virtual void Overstrum()
        {
            // Can't overstrum before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (State.NoteIndex >= Chart.Notes.Count - 1 && ActiveSustains.Count == 0)
            {
                return;
            }

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var note = ActiveSustains[i];
                ActiveSustains.Remove(note);
                i--;
                OnSustainEnd?.Invoke(note, State.CurrentTime);
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
            EngineStats.Overstrums++;

            UpdateMultiplier();

            OnOverstrum?.Invoke();
        }

        protected override bool HitNote(GuitarNote note)
        {
            note.SetHitState(true, true);

            bool skipped = false;
            var prevNote = note.PreviousNote;
            while (prevNote is not null && !prevNote.WasHit && !prevNote.WasMissed)
            {
                skipped = true;

                prevNote.SetMissState(true, true);

                EngineStats.Combo = 0;
                EngineStats.NotesMissed++;

                OnNoteMissed?.Invoke(State.NoteIndex, prevNote);
                State.NoteIndex++;

                prevNote = prevNote.PreviousNote;
            }

            if (skipped)
            {
                StripStarPower(note.PreviousNote);
            }

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

            UpdateMultiplier();

            AddScore(note);

            if (note.IsDisjoint)
            {
                foreach (var chordNote in note.ChordEnumerator())
                {
                    if (!chordNote.IsSustain)
                    {
                        continue;
                    }

                    ActiveSustains.Add(chordNote);
                    OnSustainStart?.Invoke(chordNote);
                }
            }
            else if (note.IsSustain)
            {
                ActiveSustains.Add(note);
                OnSustainStart?.Invoke(note);
            }

            State.WasNoteGhosted = false;

            OnNoteHit?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
            return true;
        }

        protected override void MissNote(GuitarNote note)
        {
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

            State.WasNoteGhosted = false;

            EngineStats.Combo = 0;
            EngineStats.NotesMissed++;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
        }

        protected override void AddScore(GuitarNote note)
        {
            EngineStats.Score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count) * EngineStats.ScoreMultiplier;
        }

        protected override void UpdateMultiplier()
        {
            EngineStats.ScoreMultiplier = EngineStats.Combo switch
            {
                >= 30 => 4,
                >= 20 => 3,
                >= 10 => 2,
                _     => 1
            };

            if (EngineStats.IsStarPowerActive)
            {
                EngineStats.ScoreMultiplier *= 2;
            }
        }

        protected override void StripStarPower(GuitarNote note)
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

            base.StripStarPower(note);
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
                score += (int) Math.Ceiling((double) note.TickLength / TicksPerSustainPoint);

                // If a note is disjoint, each sustain is counted separately.
                if (note.IsDisjoint)
                {
                    foreach (var child in note.ChildNotes)
                    {
                        score += (int) Math.Ceiling((double) child.TickLength / TicksPerSustainPoint);
                    }
                }
            }

            return score;
        }


        protected void ToggleFret(int fret, bool active)
        {
            State.ButtonMask = (byte) (active ? State.ButtonMask | (1 << fret) : State.ButtonMask & ~(1 << fret));
        }

        public bool IsFretHeld(GuitarAction fret)
        {
            return (State.ButtonMask & (1 << (int) fret)) != 0;
        }
    }
}