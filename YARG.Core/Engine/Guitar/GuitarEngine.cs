using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Logging;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarEngineParameters,
        GuitarStats, GuitarEngineState>
    {
        protected struct ActiveSustain
        {
            public GuitarNote Note;
            public uint BaseTick;
        }

        public delegate void OverstrumEvent();

        public delegate void SustainStartEvent(GuitarNote note);

        public delegate void SustainEndEvent(GuitarNote note, double timeEnded);

        public OverstrumEvent?    OnOverstrum;
        public SustainStartEvent? OnSustainStart;
        public SustainEndEvent?   OnSustainEnd;

        protected List<ActiveSustain> ActiveSustains = new();

        public override bool TreatChordAsSeparate => false;

        protected GuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
            State.Initialize(engineParameters);
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
            if (State.NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                i--;
                OnSustainEnd?.Invoke(sustain.Note, State.CurrentTime);
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

            // Detect if the last note(s) were skipped
            bool skipped = false;
            var prevNote = note.PreviousNote;
            while (prevNote is not null && !prevNote.WasHit && !prevNote.WasMissed)
            {
                skipped = true;
                MissNote(prevNote);

                EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
                {
                    NoteTime = prevNote.Time,
                    NoteLength = prevNote.TimeLength,
                    NoteIndex = State.NoteIndex,
                    NoteMask = prevNote.NoteMask,
                    WasHit = false,
                    WasSkipped = true,
                });

                prevNote = prevNote.PreviousNote;
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

                    var sustain = new ActiveSustain()
                    {
                        Note = chordNote,
                        BaseTick = chordNote.Tick,
                    };

                    ActiveSustains.Add(sustain);
                    OnSustainStart?.Invoke(chordNote);
                }
            }
            else if (note.IsSustain)
            {
                var sustain = new ActiveSustain()
                {
                    Note = note,
                    BaseTick = note.Tick,
                };

                ActiveSustains.Add(sustain);
                OnSustainStart?.Invoke(note);
            }

            State.WasNoteGhosted = false;

            EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
            {
                NoteTime = note.Time,
                NoteLength = note.TimeLength,
                NoteIndex = State.NoteIndex,
                NoteMask = note.NoteMask,
                WasHit = true,
                WasSkipped = skipped,
            });

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

            EventLogger.LogEvent(new NoteEngineEvent(State.CurrentTime)
            {
                NoteTime = note.Time,
                NoteLength = note.TimeLength,
                NoteIndex = State.NoteIndex,
                NoteMask = note.NoteMask,
                WasHit = false,
                WasSkipped = false,
            });

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            State.NoteIndex++;
        }

        protected override void AddScore(GuitarNote note)
        {
            EngineStats.CommittedScore += POINTS_PER_NOTE * (1 + note.ChildNotes.Count) * EngineStats.ScoreMultiplier;
            UpdateStars();
        }

        protected override void RebaseStarPower(uint baseTick)
        {
            base.RebaseStarPower(baseTick);

            State.StarPowerWhammyBaseTick = baseTick;
        }

        protected override double CalculateStarPowerGain(uint tick)
            => State.StarPowerWhammyTimer.IsActive(State.CurrentTime) ?
                CalculateStarPowerBeatProgress(tick, State.StarPowerWhammyBaseTick) : 0;

        protected void UpdateWhammyStarPower(bool spSustainsActive)
        {
            if (spSustainsActive)
            {
                if (IsInputUpdate && CurrentInput.GetAction<GuitarAction>() == GuitarAction.Whammy)
                {
                    // Rebase when beginning to SP whammy
                    if (!State.StarPowerWhammyTimer.IsActive(State.CurrentTime))
                    {
                        RebaseStarPower(State.CurrentTick);
                    }

                    State.StarPowerWhammyTimer.Start(State.CurrentTime);
                }
                else if (State.StarPowerWhammyTimer.IsExpired(State.CurrentTime))
                {
                    // Temporarily re-start whammy timer so that whammy gain gets calculated
                    State.StarPowerWhammyTimer.Start(State.CurrentTime);

                    // Commit final whammy gain amount
                    UpdateStarPowerAmount(State.CurrentTick);
                    RebaseStarPower(State.CurrentTick);

                    // Stop whammy gain
                    State.StarPowerWhammyTimer.Reset();
                }
            }
            // Rebase after SP whammy ends to commit the final amount to the base
            else if (State.StarPowerWhammyTimer.IsActive(State.CurrentTime))
            {
                RebaseStarPower(State.CurrentTick);
                State.StarPowerWhammyTimer.Reset();
            }
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

        protected static bool IsFretInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.GreenFret or
                    GuitarAction.RedFret or
                    GuitarAction.YellowFret or
                    GuitarAction.BlueFret or
                    GuitarAction.OrangeFret or
                    GuitarAction.White3Fret => true,
                _ => false,
            };
        }

        protected static bool IsStrumInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.StrumUp or
                    GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}