using System;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public struct ReplayHeader
    {
        public long   Magic;
        public int    ReplayVersion;
        public string ReplayChecksum;
        public int    GameVersion;
    }

    public class Replay
    {
        public ReplayHeader Header;
        public string       SongName;
        public string       ArtistName;
        public string       CharterName;
        public int          BandScore;
        public DateTime     Date;
        public HashWrapper  SongChecksum;
        public int          PlayerCount;
        public string[]     PlayerNames;

        public ReplayFrame[] Frames;
    }
}