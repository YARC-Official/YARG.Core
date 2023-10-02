namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// The float value for the pitch sang this update (as a MIDI note). <c>null</c> is none, or no input.
        /// </summary>
        public float? PitchSangThisUpdate;

        /// <summary>
        /// The song tick that this phrase was processed up to.
        /// </summary>
        public uint PhraseTicksProcessed;

        /// <summary>
        /// The amount of note ticks hit in the current phrase.
        /// </summary>
        public uint PhraseTicksHit;

        public override void Reset()
        {
            base.Reset();

            PitchSangThisUpdate = null;

            PhraseTicksProcessed = 0;
            PhraseTicksHit = 0;
        }
    }
}