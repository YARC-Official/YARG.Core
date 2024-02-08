using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsStats : BaseStats
    {
        /// <summary>
        /// The amount of note ticks that was hit by the vocalist.
        /// </summary>
        public uint VocalTicksHit;

        /// <summary>
        /// The amount of note ticks that were missed by the vocalist.
        /// </summary>
        public uint VocalTicksMissed;

        public VocalsStats()
        {
        }

        public VocalsStats(VocalsStats stats) : base(stats)
        {
            VocalTicksHit = stats.VocalTicksHit;
            VocalTicksMissed = stats.VocalTicksMissed;
        }

        public override void Reset()
        {
            base.Reset();
            VocalTicksHit = 0;
            VocalTicksMissed = 0;
        }

        public override void Serialize(IBinaryDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(VocalTicksHit);
            writer.Write(VocalTicksMissed);
        }

        public override void Deserialize(IBinaryDataReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            VocalTicksHit = reader.ReadUInt32();
            VocalTicksMissed = reader.ReadUInt32();
        }
    }
}