namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// Whether or not the player/bot sang in the current update.
        /// </summary>
        public bool DidSing;

        /// <summary>
        /// The float value for the last pitch sang (as a MIDI note).
        /// </summary>
        public float PitchSang;

        /// <summary>
        /// The amount of vocal ticks in the current phrase. Is decimal.<br/>
        /// A vocal tick is the amount of vocal updates per second.
        /// </summary>
        public double? PhraseTicksTotal;

        /// <summary>
        /// The amount of vocals ticks hit in the current phrase. Is not decimal.<br/>
        /// A vocal tick is the amount of vocal updates per second.
        /// </summary>
        public uint PhraseTicksHit;

        /// <summary>
        /// The last time there was a pitch update.
        /// </summary>
        public double LastSingTime;

        /// <summary>
        /// The last time a note was hit.
        /// </summary>
        public double LastHitTime;

        public override void Reset()
        {
            base.Reset();

            PitchSang = 0f;

            PhraseTicksTotal = null;
            PhraseTicksHit = 0;

            LastSingTime = double.NegativeInfinity;
            LastHitTime = double.NegativeInfinity;
        }
    }
}