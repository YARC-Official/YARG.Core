using System.IO;

namespace YARG.Core.Engine.ProGuitar
{
    public class ProGuitarStats : BaseStats
    {
        public ProGuitarStats()
        {
        }

        public ProGuitarStats(ProGuitarStats stats) : base(stats)
        {
            SustainScore = stats.SustainScore;
        }

        public override void Reset()
        {
            base.Reset();
            SustainScore = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(SustainScore);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            SustainScore = reader.ReadInt32();
        }
    }
}