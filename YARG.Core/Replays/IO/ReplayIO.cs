using System;
using System.IO;
using System.Linq;

namespace YARG.Core.Replays.IO
{
    public enum ReplayReadResult
    {
        Valid,
        NotAReplay,
        InvalidVersion,
        Corrupted,
    }

    public static class ReplayIO
    {
        public const long REPLAY_MAGIC_HEADER = 0x59414C5047524159;
        public const int  REPLAY_VERSION      = 3;

        // Some versions may be invalidated (such as significant format changes)
        private static readonly int[] InvalidVersions = { 0, 1, 2 };

        public static ReplayReadResult ReadReplay(string path, out ReplayFile replayFile)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            try
            {
                replayFile = ReplayFile.Create(reader);

                if (replayFile.Header.Magic != REPLAY_MAGIC_HEADER) return ReplayReadResult.NotAReplay;

                int version = replayFile.Header.ReplayVersion;
                if (InvalidVersions.Contains(version) || version > REPLAY_VERSION) return ReplayReadResult.InvalidVersion;

                replayFile.ReadData(reader, replayFile.Header.ReplayVersion);

                return ReplayReadResult.Valid;
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Failed to read replay file");
                replayFile = null;
                return ReplayReadResult.Corrupted;
            }
        }

        public static void WriteReplay(string path, Replay replay)
        {
            using var stream = File.OpenWrite(path);
            using var writer = new BinaryWriter(stream);

            try
            {
                var replayFile = new ReplayFile(replay);

                replayFile.Serialize(writer);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Failed to write replay file");
            }
        }
    }
}