namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineState : BaseEngineState
    {
        public byte LastButtonMask;
        public byte ButtonMask;

        public bool HasFretted;
        public bool HasStrummed;
        public bool HasTapped;
        public bool HasWhammied;

        public bool IsFretPress;

        public bool WasNoteGhosted;

        /// <summary>
        /// The amount of time a hopo is allowed to take a strum input.
        /// Strum after this time and it will overstrum.
        /// </summary>
        public EngineTimer HopoLeniencyTimer;

        /// <summary>
        /// The amount of time a strum can be inputted before fretting the correct note.
        /// Fretting after this time will overstrum.
        /// </summary>
        public EngineTimer StrumLeniencyTimer;

        public EngineTimer StarPowerWhammyTimer;

        public double FrontEndExpireTime;

        public void Initialize(GuitarEngineParameters parameters)
        {
            StrumLeniencyTimer = new(parameters.StrumLeniency);
            HopoLeniencyTimer = new(parameters.HopoLeniency);
            StarPowerWhammyTimer = new(parameters.StarPowerWhammyBuffer);
        }

        public override void Reset()
        {
            base.Reset();

            ButtonMask = 0;

            HasFretted = false;
            HasStrummed = false;

            WasNoteGhosted = false;

            StrumLeniencyTimer.Disable();
            HopoLeniencyTimer.Disable();
            StarPowerWhammyTimer.Disable();

            FrontEndExpireTime = 0;
        }
    }
}