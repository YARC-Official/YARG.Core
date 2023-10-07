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
        /// How often the vocals give a pitch reading (approximately).
        /// </summary>
        public double ApproximateVocalFps { get; private set; }

        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(double hitWindow, double phraseHitPercent, double approximateVocalFps,
            float[] starMultiplierThresholds)
            : base(hitWindow, 1f, starMultiplierThresholds)
        {
            PhraseHitPercent = phraseHitPercent;
            ApproximateVocalFps = approximateVocalFps;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(PhraseHitPercent);
            writer.Write(ApproximateVocalFps);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            PhraseHitPercent = reader.ReadDouble();
            ApproximateVocalFps = reader.ReadDouble();
        }
    }
}