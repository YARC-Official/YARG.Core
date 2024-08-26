using YARG.Core.Replays.Serialization;

namespace YARG.Core.Engine.Guitar
{
    public class GuitarStats : BaseStats
    {
        /// <summary>
        /// Number of overstrums which have occurred.
        /// </summary>
        public int Overstrums;

        /// <summary>
        /// Number of hammer-ons/pull-offs which have been strummed.
        /// </summary>
        public int HoposStrummed;

        /// <summary>
        /// Number of ghost inputs the player has made.
        /// </summary>
        public int GhostInputs;

        internal GuitarStats(SerializedGuitarStats guitarStats, SerializedBaseStats baseStats) : base(baseStats)
        {
            Overstrums = guitarStats.Overstrums;
            HoposStrummed = guitarStats.HoposStrummed;
            GhostInputs = guitarStats.GhostInputs;
        }

        public GuitarStats(GuitarStats stats) : base(stats)
        {
            Overstrums = stats.Overstrums;
            HoposStrummed = stats.HoposStrummed;
            GhostInputs = stats.GhostInputs;
            SustainScore = stats.SustainScore;
        }

        public override void Reset()
        {
            base.Reset();
            Overstrums = 0;
            HoposStrummed = 0;
            GhostInputs = 0;
            SustainScore = 0;
        }
    }
}