using System;
using YARG.Core.Game;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public struct ReplayMetadata
    {
        public string      SongName;
        public string      ArtistName;
        public string      CharterName;
        public int         BandScore;
        public StarAmount  BandStars;
        public double      ReplayLength;
        public DateTime    Date;
        public HashWrapper SongChecksum;
    }
}