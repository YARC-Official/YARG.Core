using System.IO;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineParameters : BaseEngineParameters
    {
        /// <summary>
        /// The total size of the pitch window. If the player sings outside of it, no hit
        /// percent is awarded.
        /// </summary>
        public float PitchWindow { get; private set; }

        /// <summary>
        /// The total size of the pitch window that awards full points. If the player sings
        /// outside of it while in the normal pitch window, the amount of fill percent
        /// awarded will decrease gradually.
        /// </summary>
        public float PitchWindowPerfect { get; private set; }

        /// <summary>
        /// The percent of ticks that have to be correct in a phrase for it to count for full points.
        /// </summary>
        public double PhraseHitPercent { get; private set; }

        /// <summary>
        /// How often the vocals give a pitch reading (approximately). This is used to determine
        /// the leniency for hit ticks.
        /// </summary>
        public double ApproximateVocalFps { get; private set; }

        /// <summary>
        /// Whether or not the player can sing to activate starpower.
        /// </summary>
        public bool SingToActivateStarPower { get; private set; }

        /// <summary>
        /// Base score awarded per complete vocal phrase.
        /// </summary>
        public int PointsPerPhrase { get; private set; }

        public VocalsEngineParameters()
        {
        }

        public VocalsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds,
            float pitchWindow, float pitchWindowPerfect, double phraseHitPercent, double approximateVocalFps,
            bool singToActivateStarPower, int pointsPerPhrase)
            : base(hitWindow, maxMultiplier, starMultiplierThresholds)
        {
            PitchWindow = pitchWindow;
            PitchWindowPerfect = pitchWindowPerfect;
            PhraseHitPercent = phraseHitPercent;
            ApproximateVocalFps = approximateVocalFps;
            SingToActivateStarPower = singToActivateStarPower;
            PointsPerPhrase = pointsPerPhrase;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(PitchWindow);
            writer.Write(PitchWindowPerfect);
            writer.Write(PhraseHitPercent);
            writer.Write(ApproximateVocalFps);
            writer.Write(SingToActivateStarPower);
            writer.Write(PointsPerPhrase);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            PitchWindow = reader.ReadSingle();
            PitchWindowPerfect = reader.ReadSingle();
            PhraseHitPercent = reader.ReadDouble();
            ApproximateVocalFps = reader.ReadDouble();
            SingToActivateStarPower = reader.ReadBoolean();
            PointsPerPhrase = reader.ReadInt32();
        }
    }
}
