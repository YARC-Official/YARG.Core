namespace YARG.Core.Engine.Drums
{
    public class DrumsEngineParameters : BaseEngineParameters
    {
        public enum DrumMode : byte
        {
            NonProFourLane,
            ProFourLane,
            FiveLane
        }

        /// <summary>
        /// What mode the inputs should be processed in.
        /// </summary>
        public DrumMode Mode;

        /// <summary>
        /// Velocity threshold for Drum note types.
        /// </summary>
        /// <remarks>
        /// Ghost notes are below the threshold, Accent notes are above the threshold.
        /// </remarks>
        public float VelocityThreshold;

        /// <summary>
        /// The maximum allowed time in seconds between notes to use context-sensitive velocity scoring.
        /// </summary>
        public float SituationalVelocityWindow;

        public DrumsEngineParameters()
        {
        }

        public DrumsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            DrumMode mode)
            : base(hitWindow, maxMultiplier, 0, 0, starMultiplierThresholds)
        {
            Mode = mode;
            VelocityThreshold = 0.35f;
            SituationalVelocityWindow = 1.5f;
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Velocity threshold: {VelocityThreshold}\n" +
                $"Situational velocity window: {SituationalVelocityWindow}";
        }
    }
}