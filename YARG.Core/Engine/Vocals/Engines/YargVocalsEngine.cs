using System;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals.Engines
{
    public class YargVocalsEngine : VocalsEngine
    {
        public YargVocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack, VocalsEngineParameters engineParameters)
            : base(chart, syncTrack, engineParameters, true)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            throw new NotImplementedException();
        }

        protected override void UpdateHitLogic(double time) => throw new NotImplementedException();

        protected override void UpdateBot(double songTime)
        {
            throw new NotImplementedException();
        }

        protected override void CheckForNoteHit() => throw new NotImplementedException();

        protected override bool CanNoteBeHit(VocalNote note) => throw new NotImplementedException();
        protected override void HitNote(VocalNote note) => throw new NotImplementedException();

        protected override void MissNote(VocalNote note)
        {
            throw new NotImplementedException();
        }

        protected override void AddScore(VocalNote note)
        {
            throw new NotImplementedException();
        }

        protected override int CalculateBaseScore() => throw new NotImplementedException();
    }
}