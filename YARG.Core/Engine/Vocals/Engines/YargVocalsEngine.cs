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

        protected override bool UpdateEngineLogic(double time) => throw new NotImplementedException();

        public override void UpdateBot(double songTime)
        {
            throw new NotImplementedException();
        }

        protected override bool CheckForNoteHit() => throw new NotImplementedException();

        protected override bool CanNoteBeHit(VocalNote note) => throw new NotImplementedException();
        protected override bool HitNote(VocalNote note) => throw new NotImplementedException();

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