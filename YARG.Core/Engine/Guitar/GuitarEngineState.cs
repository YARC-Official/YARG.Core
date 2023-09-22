namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {

        public byte ButtonMask;
        public byte LastButtonMask;
        public byte TapButtonMask;

        public bool StrummedThisUpdate;
        public bool WasHopoStrummed;
        public bool WasNoteGhosted;

        public EngineTimer StrumLeniencyTimer;
        public EngineTimer HopoLeniencyTimer;

        public double FrontEndStartTime;

        public void Initialize(GuitarEngineParameters parameters)
        {
            StrumLeniencyTimer = new(parameters.StrumLeniency);
            HopoLeniencyTimer = new(parameters.HopoLeniency);
        }

        public override void Reset()
        {
            base.Reset();

            ButtonMask = 0;
            LastButtonMask = 0;
            TapButtonMask = 0;

            StrummedThisUpdate = false;
            WasHopoStrummed = false;
            WasNoteGhosted = false;

            StrumLeniencyTimer.Reset();
            HopoLeniencyTimer.Reset();

            FrontEndStartTime = 0;
        }
    }
}