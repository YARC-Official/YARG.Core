using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.ProKeys.Engines
{
    public class YargProKeysEngine : ProKeysEngine
    {
        public YargProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack, ProKeysEngineParameters engineParameters, bool isBot) : base(chart, syncTrack, engineParameters, isBot)
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

        protected override bool CanNoteBeHit(ProKeysNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override void AddScore(ProKeysNote note)
        {
            throw new System.NotImplementedException();
        }

        protected override int CalculateBaseScore()
        {
            throw new System.NotImplementedException();
        }
    }
}