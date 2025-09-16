using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Engine.Keys
{
    public class KeysEngineParameters : BaseEngineParameters
    {
        public readonly double ChordStaggerWindow;

        public readonly double FatFingerWindow;

        public KeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double chordStaggerWindow, double fatFingerWindow)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            ChordStaggerWindow = chordStaggerWindow;
            FatFingerWindow = fatFingerWindow;
        }

        public KeysEngineParameters(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            ChordStaggerWindow = stream.Read<double>(Endianness.Little);
            FatFingerWindow = stream.Read<double>(Endianness.Little);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(ChordStaggerWindow);
            writer.Write(FatFingerWindow);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Chord stagger window: {ChordStaggerWindow}\n" +
                $"Fat finger window: {FatFingerWindow}";
        }
    }
}