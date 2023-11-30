namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {
        public const double STAR_POWER_WHAMMY_BUFFER_TIME = 0.1;

        public byte ButtonMask;
        public byte LastButtonMask;
        public byte TapButtonMask;

        public bool StrummedThisUpdate;
        public bool WasHopoStrummed;
        public bool WasNoteGhosted;

        public EngineTimer StrumLeniencyTimer;
        public EngineTimer HopoLeniencyTimer;

        public EngineTimer StarPowerWhammyTimer;

        public double FrontEndStartTime;


        public uint StarPowerWhammyBaseTick;

        public void Initialize(GuitarEngineParameters parameters)
        {
            StrumLeniencyTimer = new(parameters.StrumLeniency);
            HopoLeniencyTimer = new(parameters.HopoLeniency);
            StarPowerWhammyTimer = new(STAR_POWER_WHAMMY_BUFFER_TIME);
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
            StarPowerWhammyTimer.Reset();

            FrontEndStartTime = 0;
        }
    }
}