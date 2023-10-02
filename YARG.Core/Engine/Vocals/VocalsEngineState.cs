namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// The float value for the pitch sang this update (as a MIDI note). <c>null</c> is none, or no input.
        /// </summary>
        public float? PitchSangThisUpdate;

        public override void Reset()
        {
            base.Reset();

            PitchSangThisUpdate = null;
        }
    }
}