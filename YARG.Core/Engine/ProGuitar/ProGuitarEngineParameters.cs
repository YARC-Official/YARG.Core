using System.IO;

namespace YARG.Core.Engine.ProGuitar
{
    public class ProGuitarEngineParameters : BaseEngineParameters
    {
        public double HopoLeniency { get; private set; }

        public double ChordStrumLeniency { get; private set; }

        public ProGuitarEngineParameters()
        {
        }

        public ProGuitarEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, double hopoLeniency, double chordStrumLeniency)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds)
        {
            HopoLeniency = hopoLeniency;
            ChordStrumLeniency = chordStrumLeniency;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(HopoLeniency);
            writer.Write(ChordStrumLeniency);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            HopoLeniency = reader.ReadDouble();
            ChordStrumLeniency = reader.ReadDouble();
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Hopo leniency: {HopoLeniency}\n" +
                $"Chord strum leniency: {ChordStrumLeniency}";
        }
    }
}