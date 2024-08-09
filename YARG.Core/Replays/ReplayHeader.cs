using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.Replays
{
    public struct ReplayHeader
    {
        public const int SIZE = 0x20;

        public EightCC     Magic;
        public short       ReplayVersion;
        public short       EngineVersion;
        public HashWrapper ReplayChecksum;

        public bool IsDevelopmentVersion()
        {
            return (ReplayVersion & 0x8000) != 0 || (EngineVersion & 0x8000) != 0;
        }
    }
}