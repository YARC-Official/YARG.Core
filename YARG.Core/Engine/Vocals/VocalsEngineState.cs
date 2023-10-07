using YARG.Core.Chart;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// The float value for the pitch sang this update (as a MIDI note). <c>null</c> is none, or no input.
        /// </summary>
        public float PitchSangThisUpdate;

        /// <summary>
        /// The amount of note ticks in the current phrase.
        /// </summary>
        public uint? PhraseTicksTotal;

        /// <summary>
        /// The amount of note ticks hit in the current phrase.
        /// </summary>
        public uint PhraseTicksHit;

        /// <summary>
        /// The ratio of vocal reading FPS to MIDI resolution.
        /// </summary>
        public double VocalFpsToResolutionRatio;

        public void Initialize(VocalsEngineParameters parameters, SyncTrack syncTrack)
        {
            VocalFpsToResolutionRatio = parameters.ApproximateVocalFps / syncTrack.Resolution;
        }

        public override void Reset()
        {
            base.Reset();

            PitchSangThisUpdate = 0f;

            PhraseTicksTotal = null;
            PhraseTicksHit = 0;
        }
    }
}