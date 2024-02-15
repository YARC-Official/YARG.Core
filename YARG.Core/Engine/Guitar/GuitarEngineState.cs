namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {
        // Dummy variable for now
        public byte ButtonMask;

        public byte FretMask;
        public bool DidFret;

        public bool DidStrum;
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
            StarPowerWhammyTimer = new(parameters.StarPowerWhammyBuffer);
        }

        public override void Reset()
        {
            base.Reset();

            FretMask = 0;
            DidFret = false;

            DidStrum = false;
            WasHopoStrummed = false;
            WasNoteGhosted = false;

            StrumLeniencyTimer.Reset();
            HopoLeniencyTimer.Reset();
            StarPowerWhammyTimer.Reset();

            FrontEndStartTime = 0;

            StarPowerWhammyBaseTick = 0;
        }
    }
}