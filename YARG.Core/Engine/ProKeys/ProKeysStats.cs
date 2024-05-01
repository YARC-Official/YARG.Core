using System.IO;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        public ProKeysStats()
        {
        }

        public ProKeysStats(ProKeysStats stats) : base(stats)
        {
            Overhits = stats.Overhits;
        }

        public override void Reset()
        {
            base.Reset();
            Overhits = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Overhits);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            Overhits = reader.ReadInt32();
        }
    }
}