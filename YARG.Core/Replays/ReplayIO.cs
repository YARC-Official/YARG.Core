using System;
using System.IO;
using System.Linq;
using YARG.Core.IO;
using YARG.Core.Song;

namespace YARG.Core.Replays
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
        public static readonly EightCC REPLAY_MAGIC_HEADER = new('Y', 'A', 'R', 'G', 'P', 'L', 'A', 'Y');

        public const int REPLAY_VERSION = 4;

        // Some versions may be invalidated (such as significant format changes)
        private static readonly int[] InvalidVersions =
        {
            0, 1, 2, 3
        };

        // note: [NotNullWhen(ReplayReadResult.Valid)] is not a valid form of [NotNullWhen],
        // so replayFile will always be indicated as possibly being null
        public static ReplayReadResult ReadReplay(string path, out ReplayFile? replayFile)
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

        public static HashWrapper? WriteReplay(string path, Replay replay)
        {
            using var stream = File.OpenWrite(path);
            using var writer = new BinaryWriter(stream);

            try
            {
                var replayFile = new ReplayFile(replay);

                replayFile.Serialize(writer);
                return replayFile.Header.ReplayChecksum;
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Failed to write replay file");
            }

            return null;
        }
    }
}