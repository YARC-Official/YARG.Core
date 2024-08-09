using System;
using YARG.Core.Game;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public class ReplayMetadata
    {
        public string      SongName    = string.Empty;
        public string      ArtistName  = string.Empty;
        public string      CharterName = string.Empty;
        public int         BandScore;
        public StarAmount  BandStars;
        public double      ReplayLength;
        public DateTime    Date;
        public HashWrapper SongChecksum;
    }
}