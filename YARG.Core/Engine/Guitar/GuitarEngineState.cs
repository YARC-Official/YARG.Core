namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {

        public byte ButtonMask;
        public byte TapButtonMask;

        public bool StrummedThisUpdate;
        public bool WasHopoStrummed;

        public double StrumLeniencyTimer;
        public double HopoLeniencyTimer;

        public double FrontEndTimer;

        public override void Reset()
        {
            base.Reset();

            ButtonMask = 0;
            StrummedThisUpdate = false;
            WasHopoStrummed = false;

            StrumLeniencyTimer = 0;
            HopoLeniencyTimer = 0;

            FrontEndTimer = 0;
        }
    }
}