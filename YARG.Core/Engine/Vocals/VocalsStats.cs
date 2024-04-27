using System.IO;

namespace YARG.Core.Engine.Vocals
{
    public class VocalsStats : BaseStats
    {
        /// <summary>
        /// The amount of note ticks that was hit by the vocalist.
        /// </summary>
        public uint TicksHit;

        /// <summary>
        /// The amount of note ticks that were missed by the vocalist.
        /// </summary>
        public uint TicksMissed;

        /// <summary>
        /// The total amount of note ticks.
        /// </summary>
        public uint TotalTicks => TicksHit + TicksMissed;

        public override float Percent => (float) TicksHit / TotalTicks;

        public VocalsStats()
        {
        }

        public VocalsStats(VocalsStats stats) : base(stats)
        {
            TicksHit = stats.TicksHit;
            TicksMissed = stats.TicksMissed;
        }

        public override void Reset()
        {
            base.Reset();
            TicksHit = 0;
            TicksMissed = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(TicksHit);
            writer.Write(TicksMissed);
        }

        public override void Deserialize(BinaryReader reader, int version = 0)
        {
            base.Deserialize(reader, version);

            TicksHit = reader.ReadUInt32();
            TicksMissed = reader.ReadUInt32();
        }
    }
}