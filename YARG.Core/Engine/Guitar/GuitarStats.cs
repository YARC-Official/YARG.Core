namespace YARG.Core.Engine.Guitar
{
    public class GuitarStats : BaseStats
    {
        public int Overstrums;
        public int HoposStrummed;
        public int GhostInputs;

        public GuitarStats()
        {
        }

        public GuitarStats(GuitarStats stats) : base(stats)
        {
            Overstrums = stats.Overstrums;
            HoposStrummed = stats.HoposStrummed;
            GhostInputs = stats.GhostInputs;
        }
    }
}