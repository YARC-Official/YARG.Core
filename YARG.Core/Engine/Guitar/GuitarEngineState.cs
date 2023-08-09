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

        public double StrumLeniencyStartTime;
        public double HopoLeniencyStartTime;

        public double FrontEndStartTime;

        public override void Reset()
        {
            base.Reset();

            ButtonMask = 0;
            LastButtonMask = 0;
            TapButtonMask = 0;

            StrummedThisUpdate = false;
            WasHopoStrummed = false;
            WasNoteGhosted = false;

            StrumLeniencyStartTime = double.MaxValue;
            HopoLeniencyStartTime = double.MaxValue;

            FrontEndStartTime = 0;
        }
    }
}