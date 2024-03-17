namespace YARG.Core.Engine.Guitar
{
    public enum FretState : byte
    {
        None,
        Down,
        Up
    }

    public class GuitarEngineState : BaseEngineState
    {
        // Dummy variable for now
        public byte ButtonMask;

        public byte LastFretMask;
        public byte FretMask;

        public bool HasFretted;
        public bool IsFretPress;

        public bool HasStrummed;
        public bool HasWhammied;

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
        /// <summary>
        /// The time at which the note should be hit for infinite front end. If null,
        /// the note should not be hit without an input.
        /// </summary>
        public double? InfiniteFrontEndHitTime;

        public EngineTimer StarPowerWhammyTimer;

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

            LastFretMask = 0;
            FretMask = 0;

            HasFretted = false;
            IsFretPress = false;

            HasStrummed = false;
            HasWhammied = false;

            WasNoteGhosted = false;

            StrumLeniencyTimer.Reset();
            HopoLeniencyTimer.Reset();
            StarPowerWhammyTimer.Reset();

            StarPowerWhammyBaseTick = 0;
        }
    }
}