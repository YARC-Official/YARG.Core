using System.IO;

namespace YARG.Core.Engine.ProGuitar
{
    public class ProGuitarEngineParameters : BaseEngineParameters
    {
        public ProGuitarEngineParameters()
        {
        }

        public ProGuitarEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n";
        }
    }
}