using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack, GuitarEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters, true)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            throw new System.NotImplementedException();
        }

        protected override bool UpdateEngineLogic(double time) => throw new System.NotImplementedException();

        public override void UpdateBot(double songTime)
        {
            throw new System.NotImplementedException();
        }

        protected override bool CheckForNoteHit() => throw new System.NotImplementedException();

        protected override bool CanNoteBeHit(GuitarNote note) => throw new System.NotImplementedException();
        protected override bool HitNote(GuitarNote note) => throw new System.NotImplementedException();

        protected override void MissNote(GuitarNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override void AddScore(GuitarNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override int CalculateBaseScore() => throw new System.NotImplementedException();
    }
}