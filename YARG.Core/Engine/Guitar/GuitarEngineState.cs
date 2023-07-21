namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {

        public byte ButtonMask;
        public byte TapButtonMask;

        public bool StrummedThisUpdate;
        public bool WasHopoStrummed;

        public double StrumLeniencyStartTime;
        public double HopoLeniencyStartTime;

        public double FrontEndStartTime;

        public override void Reset()
        {
            base.Reset();

            ButtonMask = 0;
            StrummedThisUpdate = false;
            WasHopoStrummed = false;

            StrumLeniencyStartTime = 0;
            HopoLeniencyStartTime = 0;

            FrontEndStartTime = 0;
        }
    }
}