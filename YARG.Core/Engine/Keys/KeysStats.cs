using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Replays;

namespace YARG.Core.Engine.ProKeys
{
    public class KeysStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        /// <summary>
        /// Amount of overhits which were ignored due to fat-fingering.
        /// </summary>
        public int FatFingersIgnored;

        public KeysStats()
        {
        }

        public KeysStats(KeysStats stats) : base(stats)
        {
            Overhits = stats.Overhits;
            FatFingersIgnored = stats.FatFingersIgnored;
        }

        public KeysStats(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            Overhits = stream.Read<int>(Endianness.Little);
            FatFingersIgnored = stream.Read<int>(Endianness.Little);
        }

        public override void Reset()
        {
            base.Reset();
            Overhits = 0;
            FatFingersIgnored = 0;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Overhits);
            writer.Write(FatFingersIgnored);
        }

        public override ReplayStats ConstructReplayStats(string name)
        {
            return new ProKeysReplayStats(name, this);
        }
    }
}