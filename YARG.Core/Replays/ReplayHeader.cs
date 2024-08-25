using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public struct ReplayHeader
    {
        public const int SIZE = 0x24;

        public static readonly EightCC REPLAY_MAGIC_HEADER = new('Y', 'A', 'R', 'G', 'P', 'L', 'A', 'Y');

        public EightCC     Magic;
        public int         ReplayVersion;
        public int         EngineVersion;
        public HashWrapper ReplayChecksum;

        public bool IsDevelopmentReplay() => (ReplayVersion & 0x8000_0000) != 0;

        public bool IsDevelopmentEngine() => (EngineVersion & 0x8000_0000) != 0;
    }
}