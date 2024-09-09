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
                HeldFrets[(int) action] = (byte) gameInput.Integer;
            }
            else if (action is >= ProGuitarAction.String1_Strum and <= ProGuitarAction.String6_Strum)
            {
                int index = action - ProGuitarAction.String1_Strum;

                // Strum works on protar by sending the "velocity" of the string hit. If there is a change in this value,
                // we can call it a strum. The "velocity" value is based upon a sensor on each string which outputs a pretty
                // much random value on strum.
                if (LastStrumValues[index] != gameInput.Integer)
                {
                    LastStrumValues[index] = (byte) gameInput.Integer;
                    Strums |= (byte) (1 << index);
                }
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