namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// Whether or not the player/bot has hit their mic in the current update.
        /// </summary>
        public bool HasHit;

        /// <summary>
        /// Whether or not the player/bot sang in the current update.
        /// </summary>
        public bool HasSang;

        /// <summary>
        /// The float value for the last pitch sang (as a MIDI note).
        /// </summary>
        public float PitchSang;

        /// <summary>
        /// The amount of ticks in the current phrase.
        /// </summary>
        public uint? PhraseTicksTotal;

        /// <summary>
        /// The amount of ticks hit in the current phrase.
        /// This is a decimal since you can get fractions of a point for singing slightly off.
        /// </summary>
        public double PhraseTicksHit;

        /// <summary>
        /// The last tick where there was a successful sing input.
        /// </summary>
        public uint LastSingTick;

        public override void Reset()
        {
            base.Reset();

            HasSang = false;
            PitchSang = 0f;

            PhraseTicksTotal = null;
            PhraseTicksHit = 0;

            LastSingTick = 0;
        }
    }
}