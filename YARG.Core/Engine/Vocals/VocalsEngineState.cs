using YARG.Core.Chart;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// The float value for the last pitch sang (as a MIDI note).
        /// </summary>
        public float PitchSang;

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

        /// <summary>
        /// The last time there was a pitch update. <b>THIS IS FOR VISUAL PURPOSES ONLY.</b>
        /// </summary>
        public double VisualLastSingTime;

        /// <summary>
        /// The last time a note was hit. <b>THIS IS FOR VISUAL PURPOSES ONLY.</b>
        /// </summary>
        public double VisualLastHitTime;

        public void Initialize(VocalsEngineParameters parameters, SyncTrack syncTrack)
        {
            VocalFpsToResolutionRatio = parameters.ApproximateVocalFps / syncTrack.Resolution;
        }

        public override void Reset()
        {
            base.Reset();

            PitchSang = 0f;

            PhraseTicksTotal = null;
            PhraseTicksHit = 0;

            VisualLastSingTime = double.NegativeInfinity;
            VisualLastHitTime = double.NegativeInfinity;
        }
    }
}