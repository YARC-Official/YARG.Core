using YARG.Core.Song;

namespace YARG.Core.IO
{
    public static class RatingHelper
    {
        public static SongRating ParseSongRating(uint rating)
        {
            return rating switch
            {
                0 => SongRating.Unspecified,
                1 => SongRating.Family_Friendly,
                2 => SongRating.Supervision_Recommended,
                3 => SongRating.Mature,
                4 => SongRating.No_Rating,
                5 => SongRating.Sensitive_Content,
                _ => SongRating.Unspecified
            };
        }
    }
}
