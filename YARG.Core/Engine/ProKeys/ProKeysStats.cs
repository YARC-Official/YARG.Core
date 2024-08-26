using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine.ProKeys
{
    public class ProKeysStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        internal ProKeysStats(SerializedProKeysStats proKeysStats, SerializedBaseStats baseStats) : base(baseStats)
        {
            Overhits = proKeysStats.Overhits;
        }

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
    }
}