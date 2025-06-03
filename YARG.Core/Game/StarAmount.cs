namespace YARG.Core.Game
{
    public enum StarAmount : byte
    {
        None = 0,

        Star1 = 1,
        Star2 = 2,
        Star3 = 3,
        Star4 = 4,
        Star5 = 5,

        StarGold,
        StarSilver,
        StarBrutal,

        NoPart
    }

    public static class StarAmountHelper
    {
        public static StarAmount GetStarsFromInt(int stars)
        {
            return stars switch
            {
                >= 0 and <= 5 => (StarAmount) stars,
                6             => StarAmount.StarGold,
                _             => StarAmount.None
            };
        }

        public static int GetStarCount(this StarAmount starAmount)
        {
            return starAmount switch
            {
                <= StarAmount.Star5 => (int)starAmount,
                _                   => 5,
            };
        }

        public static int GetSortWeight(this StarAmount stars)
        {
            return stars switch
            {
                StarAmount.StarGold => 6,
                StarAmount.Star5    => 5,
                StarAmount.Star4    => 4,
                StarAmount.Star3    => 3,
                StarAmount.Star2    => 2,
                StarAmount.Star1    => 1,
                StarAmount.None     => 0,
                StarAmount.NoPart   => -1,
                _ => -2
            };
        }

        public static string GetDisplayName(this StarAmount stars)
        {
            return stars switch
            {
                StarAmount.StarGold => "Gold Stars",
                StarAmount.Star5    => "5 Stars",
                StarAmount.Star4    => "4 Stars",
                StarAmount.Star3    => "3 Stars",
                StarAmount.Star2    => "2 Stars",
                StarAmount.Star1    => "1 Star",
                StarAmount.None     => "UNPLAYED SONGS",
                StarAmount.NoPart   => "NO PART",
                _                   => "Other"
            };
        }
    }
}