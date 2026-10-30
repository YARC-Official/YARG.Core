using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineParameters : BaseEngineParameters
    {
        /// <summary>
        /// The total size of the pitch window. If the player sings outside of it, no hit
        /// percent is awarded.
        /// </summary>
        public readonly float PitchWindow;

        /// <summary>
        /// The total size of the pitch window that awards full points. If the player sings
        /// outside of it while in the normal pitch window, the amount of fill percent
        /// awarded will decrease gradually.
        /// </summary>
        public readonly float PitchWindowPerfect;

        /// <summary>
        /// The percent of ticks that have to be correct in a phrase for it to count for full points.
        /// </summary>
        public readonly double PhraseHitPercent;

        /// <summary>
        /// How often the vocals give a pitch reading (approximately). This is used to determine
        /// the leniency for hit ticks.
        /// </summary>
        public readonly double ApproximateVocalFps;

        /// <summary>
        /// Whether or not the player can sing to activate starpower.
        /// </summary>
        public readonly bool SingToActivateStarPower;

        /// <summary>
        /// Base score awarded per complete vocal phrase.
        /// </summary>
        public readonly int PointsPerPhrase;

        public const int MAX_PARTY_MIC_COUNT = 7;
        public readonly int PartyVocalsMicCount;
        public readonly Instrument PartyVocalsChartSource;

        public VocalsEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, float[] starMultiplierThresholds, float[] soloBonusStarMultiplierThresholds,
            float pitchWindow, float pitchWindowPerfect, double phraseHitPercent, double approximateVocalFps,
            bool singToActivateStarPower, int pointsPerPhrase, int partyVocalsMicCount = 1,
            Instrument partyVocalsChartSource = Instrument.Harmony)
            : base(hitWindow, maxMultiplier, 0, 0, starMultiplierThresholds, soloBonusStarMultiplierThresholds, enableLanes: false)
        {
            PitchWindow = pitchWindow;
            PitchWindowPerfect = pitchWindowPerfect;
            PhraseHitPercent = phraseHitPercent;
            ApproximateVocalFps = approximateVocalFps;
            SingToActivateStarPower = singToActivateStarPower;
            PointsPerPhrase = pointsPerPhrase;
            ValidatePartySession(partyVocalsMicCount, partyVocalsChartSource);
            PartyVocalsMicCount = partyVocalsMicCount;
            PartyVocalsChartSource = partyVocalsChartSource;
        }

        public VocalsEngineParameters(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            PitchWindow = stream.Read<float>(Endianness.Little);
            PitchWindowPerfect = stream.Read<float>(Endianness.Little);
            PhraseHitPercent = stream.Read<double>(Endianness.Little);
            ApproximateVocalFps = stream.Read<double>(Endianness.Little);
            SingToActivateStarPower = stream.ReadBoolean();
            PointsPerPhrase = stream.Read<int>(Endianness.Little);
            PartyVocalsMicCount = version >= 20 ? stream.ReadByte() : 1;
            PartyVocalsChartSource = version >= 20 ? (Instrument) stream.ReadByte() : Instrument.Harmony;
            ValidatePartySession(PartyVocalsMicCount, PartyVocalsChartSource);
        }

        private static void ValidatePartySession(int count, Instrument source)
        {
            if (count < 1 || count > MAX_PARTY_MIC_COUNT)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (source != Instrument.Vocals && source != Instrument.Harmony)
                throw new ArgumentOutOfRangeException(nameof(source));
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
            writer.Write((byte) PartyVocalsMicCount);
            writer.Write((byte) PartyVocalsChartSource);
        }
    }
}
