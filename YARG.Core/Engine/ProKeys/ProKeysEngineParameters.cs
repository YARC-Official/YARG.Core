using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysEngineParameters : BaseEngineParameters
    {
        public readonly double ChordStaggerWindow;

        public readonly double FatFingerWindow;

        public readonly bool NoStarPowerOverlap;

        public ProKeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double chordStaggerWindow, double fatFingerWindow,
            bool noStarPowerOverlap)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            ChordStaggerWindow = chordStaggerWindow;
            FatFingerWindow = fatFingerWindow;
            NoStarPowerOverlap = noStarPowerOverlap;
        }

        public ProKeysEngineParameters(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            ChordStaggerWindow = stream.Read<double>(Endianness.Little);
            FatFingerWindow = stream.Read<double>(Endianness.Little);
            if (version >= 9) {
                NoStarPowerOverlap = stream.ReadBoolean();
            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(ChordStaggerWindow);
            writer.Write(FatFingerWindow);
            writer.Write(NoStarPowerOverlap);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Chord stagger window: {ChordStaggerWindow}\n" +
                $"Fat finger window: {FatFingerWindow}\n" +
                $"No star power overlap: {NoStarPowerOverlap}";
        }
    }
}