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

        public DrumsEngineParameters()
        {
        }

        public DrumsEngineParameters(HitWindowSettings hitWindow, float[] starMultiplierThresholds,
            DrumMode mode)
            : base(hitWindow, starMultiplierThresholds)
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