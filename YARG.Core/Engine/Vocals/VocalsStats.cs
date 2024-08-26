using YARG.Core.Replays.Serialization;

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

        public override float Percent => TotalTicks == 0 ? 1f : (float) TicksHit / TotalTicks;

        internal VocalsStats(SerializedVocalsStats vocalsStats, SerializedBaseStats baseStats) : base(baseStats)
        {
            TicksHit = vocalsStats.TicksHit;
            TicksMissed = vocalsStats.TicksMissed;
        }

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
    }
}