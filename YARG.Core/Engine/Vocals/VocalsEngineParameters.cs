using System.IO;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineParameters : BaseEngineParameters
    {
        /// <summary>
        /// The percent of ticks that have to be correct in a phrase for it to count as a hit.
        /// </summary>
        public double PhraseHitPercent { get; private set; }

        /// <summary>
        /// How long the pitch input should continue being held onto for.
        /// </summary>
        public double InputLeniency { get; private set; }

        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(double hitWindow, double phraseHitPercent, double inputLeniency,
            float[] starMultiplierThresholds)
            : base(hitWindow, 1f, starMultiplierThresholds)
        {
            PhraseHitPercent = phraseHitPercent;
            InputLeniency = inputLeniency;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(PhraseHitPercent);
            writer.Write(InputLeniency);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            PhraseHitPercent = reader.ReadDouble();
            InputLeniency = reader.ReadDouble();
        }
    }
}