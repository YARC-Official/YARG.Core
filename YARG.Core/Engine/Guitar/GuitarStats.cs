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

        /// <summary>
        /// Score earned from holding sustains.
        /// </summary>
        public int SustainScore;

        /// <summary>
        /// Amount of Star Power ticks gained from whammy.
        /// </summary>
        public uint WhammyTicks;


        public GuitarStats()
        {
        }

        public GuitarStats(GuitarStats stats) : base(stats)
        {
            Overstrums = stats.Overstrums;
            HoposStrummed = stats.HoposStrummed;
            GhostInputs = stats.GhostInputs;
            WhammyTicks = stats.WhammyTicks;
            SustainScore = stats.SustainScore;
        }

        public override void Reset()
        {
            base.Reset();
            Overstrums = 0;
            HoposStrummed = 0;
            GhostInputs = 0;
            WhammyTicks = 0;
            SustainScore = 0;
        }
    }
}