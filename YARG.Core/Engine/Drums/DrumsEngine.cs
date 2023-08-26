using System;
using System.Diagnostics;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums
{
    public abstract class DrumsEngine : BaseEngine<DrumNote, DrumsAction, DrumsEngineParameters,
        DrumsStats, DrumsEngineState>
    {
        public delegate void OverhitEvent();

        public OverhitEvent OnOverhit;

        protected sealed override float[] StarMultiplierThresholds { get; } =
            { 0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f };

        protected sealed override float[] StarScoreThresholds { get; }

        protected DrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack,
            DrumsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters)
        {
            BaseScore = CalculateBaseScore();

            StarScoreThresholds = new float[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = BaseScore * StarMultiplierThresholds[i];
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            base.Reset(keepCurrentButtons);
        }

        public virtual void Overhit()
        {
            // Can't overhit before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overhit if past last note
            if (State.NoteIndex >= Chart.Notes.Count - 1)
            {
                return;
            }

            EngineStats.Combo = 0;
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke();
        }

        protected override bool HitNote(DrumNote note)
        {
            note.SetHitState(true, false);

            // Detect if the last note(s) were skipped
            // bool skipped = false;
            // var prevNote = note.PreviousNote;
            // while (prevNote is not null && !prevNote.WasFullyHitOrMissed())
            // {
            //     YargTrace.LogInfo("BAD");
            //
            //     skipped = true;
            //     EngineStats.Combo = 0;
            //
            //     foreach (var chordNote in prevNote.ChordEnumerator())
            //     {
            //         if (chordNote.WasMissed || chordNote.WasHit)
            //         {
            //             continue;
            //         }
            //
            //         chordNote.SetMissState(true, false);
            //         EngineStats.NotesMissed++;
            //         OnNoteMissed?.Invoke(State.NoteIndex, prevNote);
            //     }
            //
            //     State.NoteIndex++;
            //     prevNote = prevNote.PreviousNote;
            // }
            //
            // if (skipped)
            // {
            //     StripStarPower(note.PreviousNote);
            // }

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

            OnNoteHit?.Invoke(State.NoteIndex, note);

            if (note.ParentOrSelf.WasFullyHitOrMissed())
            {
                State.NoteIndex++;
            }

            return true;
        }

        protected override void MissNote(DrumNote note)
        {
            YargTrace.LogInfo($"Missed note at tick {note.Tick}");
            note.SetMissState(true, false);

            // if (note.IsStarPower)
            // {
            //     StripStarPower(note);
            // }
            //
            // if (note.IsSoloEnd)
            // {
            //     EndSolo();
            // }
            // if (note.IsSoloStart)
            // {
            //     StartSolo();
            // }

            EngineStats.Combo = 0;
            EngineStats.NotesMissed++;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);

            if (note.ParentOrSelf.WasFullyHitOrMissed())
            {
                State.NoteIndex++;
            }
        }

        protected override void AddScore(DrumNote note)
        {
            EngineStats.Score += POINTS_PER_NOTE * EngineStats.ScoreMultiplier;
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

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
            }

            return score;
        }

        protected static bool IsTomInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() switch
            {
                DrumsAction.RedDrum or
                    DrumsAction.YellowDrum or
                    DrumsAction.BlueDrum or
                    DrumsAction.GreenDrum => true,
                _ => false,
            };
        }

        protected static bool IsCymbalInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() switch
            {
                DrumsAction.YellowCymbal or
                    DrumsAction.BlueCymbal or
                    DrumsAction.OrangeCymbal or
                    DrumsAction.GreenCymbal => true,
                _ => false,
            };
        }

        protected static bool IsKickInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() == DrumsAction.Kick;
        }
    }
}