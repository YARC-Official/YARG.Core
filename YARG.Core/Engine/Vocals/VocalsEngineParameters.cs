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

        /// <summary>
        /// Whether or not the player can sing to activate starpower.
        /// </summary>
        public bool SingToActivateStarPower { get; private set; }

        public int PointsPerPhrase { get; private set; }

        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            double phraseHitPercent, bool singToActivateStarPower, double approximateVocalFps, int pointsPerPhrase)
            : base(hitWindow, maxMultiplier, starMultiplierThresholds)
        {
            PhraseHitPercent = phraseHitPercent;
            ApproximateVocalFps = approximateVocalFps;
            SingToActivateStarPower = singToActivateStarPower;
            PointsPerPhrase = pointsPerPhrase;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(PhraseHitPercent);
            writer.Write(ApproximateVocalFps);
            writer.Write(SingToActivateStarPower);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            PhraseHitPercent = reader.ReadDouble();
            ApproximateVocalFps = reader.ReadDouble();
            SingToActivateStarPower = reader.ReadBoolean();
        }
    }
}