using System.IO;

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
        public DrumMode Mode { get; private set; }

        //Ghost notes are below this threshold, Accent notes are above 1 - threshold
        public float VelocityThreshold = 0.35f;
        
        // The maximum allowed time (seconds) between notes to use context-sensitive velocity scoring
        public float SituationalVelocityWindow = 1.5f;

        public DrumsEngineParameters()
        {
        }

        public DrumsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            DrumMode mode)
            : base(hitWindow, maxMultiplier, starMultiplierThresholds)
        {
            Mode = mode;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write((byte) Mode);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            Mode = (DrumMode) reader.ReadByte();
        }
    }
}