using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public struct ReplayHeader
    {
        public EightCC     Magic;
        public short         ReplayVersion;
        public short         EngineVersion;
        public HashWrapper ReplayChecksum;
    }
}