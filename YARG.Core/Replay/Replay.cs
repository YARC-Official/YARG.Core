using System;

namespace YARG.Core.Replay
{
    public struct Replay
    {
        public long     Magic;
        public int      Version;
        public string   SongName;
        public string   ArtistName;
        public string   CharterName;
        public int      BandScore;
        public DateTime Date;
        public string   Checksum;
        public int      PlayerCount;
        public string[] PlayerNames;

        public BaseReplayFrame[] Frames;
    }
}