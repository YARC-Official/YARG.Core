using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine.Drums
{
    public class DrumsStats : BaseStats
    {
        /// <summary>
        /// Number of overhits which have occurred.
        /// </summary>
        public int Overhits;

        internal DrumsStats(SerializedDrumsStats drumsStats, SerializedBaseStats baseStats) : base(baseStats)
        {
            Overhits = drumsStats.Overhits;
        }

        public DrumsStats(DrumsStats stats) : base(stats)
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