namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineParameters : BaseEngineParameters
    {
        /// <summary>
        /// The total size of the pitch window. If the player sings outside of it, no hit
        /// percent is awarded.
        /// </summary>
        public float PitchWindow;

        /// <summary>
        /// The total size of the pitch window that awards full points. If the player sings
        /// outside of it while in the normal pitch window, the amount of fill percent
        /// awarded will decrease gradually.
        /// </summary>
        public float PitchWindowPerfect;

        /// <summary>
        /// The percent of ticks that have to be correct in a phrase for it to count for full points.
        /// </summary>
        public double PhraseHitPercent;

        /// <summary>
        /// How often the vocals give a pitch reading (approximately). This is used to determine
        /// the leniency for hit ticks.
        /// </summary>
        public double ApproximateVocalFps;

        /// <summary>
        /// Whether or not the player can sing to activate starpower.
        /// </summary>
        public bool SingToActivateStarPower;

        /// <summary>
        /// Base score awarded per complete vocal phrase.
        /// </summary>
        public int PointsPerPhrase;

        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            float pitchWindow, float pitchWindowPerfect, double phraseHitPercent, double approximateVocalFps,
            bool singToActivateStarPower, int pointsPerPhrase)
            : base(hitWindow, maxMultiplier, 0, 0, starMultiplierThresholds)
        {
            PitchWindow = pitchWindow;
            PitchWindowPerfect = pitchWindowPerfect;
            PhraseHitPercent = phraseHitPercent;
            ApproximateVocalFps = approximateVocalFps;
            SingToActivateStarPower = singToActivateStarPower;
            PointsPerPhrase = pointsPerPhrase;
        }
    }
}
