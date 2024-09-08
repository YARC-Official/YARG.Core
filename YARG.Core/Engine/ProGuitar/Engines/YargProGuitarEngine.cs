using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.ProGuitar.Engines
{
    public class YargProGuitarEngine : ProGuitarEngine
    {
        public YargProGuitarEngine(InstrumentDifficulty<ProGuitarNote> chart, SyncTrack syncTrack,
            ProGuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override void UpdateBot(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<ProGuitarAction>();

            // Star power
            if (action is ProGuitarAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
            else if (action <= ProGuitarAction.String6_Fret)
            {
                HeldFrets[(int) action] = gameInput.Integer;
            }
            else if (action is >= ProGuitarAction.String1_Strum and <= ProGuitarAction.String6_Strum)
            {
                // TODO
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            // throw new System.NotImplementedException();
        }

        protected override void CheckForNoteHit()
        {
            throw new System.NotImplementedException();
        }

        protected override bool CanNoteBeHit(ProGuitarNote note) => throw new System.NotImplementedException();

        protected override bool CanSustainHold(ProGuitarNote note) => throw new System.NotImplementedException();
    }
}