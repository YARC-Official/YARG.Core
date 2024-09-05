using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.ProGuitar
{
    public class ProGuitarEngine : BaseEngine<ProGuitarNote, ProGuitarEngineParameters,
        ProGuitarStats>
    {
        public ProGuitarEngine(InstrumentDifficulty<ProGuitarNote> chart, SyncTrack syncTrack,
            ProGuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
        }

        protected override void UpdateBot(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateHitLogic(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override void CheckForNoteHit()
        {
            throw new System.NotImplementedException();
        }

        protected override bool CanNoteBeHit(ProGuitarNote note) => throw new System.NotImplementedException();

        protected override bool CanSustainHold(ProGuitarNote note) => throw new System.NotImplementedException();

        protected override void AddScore(ProGuitarNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override int CalculateBaseScore() => throw new System.NotImplementedException();
    }
}