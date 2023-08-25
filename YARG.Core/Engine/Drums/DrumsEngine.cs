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
        }

        protected override bool HitNote(DrumNote note) => throw new System.NotImplementedException();

        protected override void MissNote(DrumNote note)
        {
            throw new System.NotImplementedException();
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