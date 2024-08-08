using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Utility;

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

        public const short REPLAY_VERSION = 6;
        public const short ENGINE_VERSION = 1;

        private const short PRE_REFACTOR_ENGINE_VERSION = 5;

        // Some versions may be invalidated (such as significant format changes)
        private static readonly short[] InvalidVersions =
        {
            0, 1, 2, 3
        };

        public static ReplayReadResult ReadReplay(string path, out Replay? replay)
        {
            replay = null;
            if (!File.Exists(path))
            {
                return ReplayReadResult.NotAReplay;
            }

            byte[] data = File.ReadAllBytes(path);

            int fileVersion;

            unsafe
            {
                fixed(byte* ptr = data)
                {
                    var header = Unsafe.Read<ReplayHeader>(ptr);

                    if (header.Magic != REPLAY_MAGIC_HEADER)
                    {
                        return ReplayReadResult.NotAReplay;
                    }

                    if (InvalidVersions.Contains(header.ReplayVersion) || header.ReplayVersion > REPLAY_VERSION)
                    {
                        return ReplayReadResult.InvalidVersion;
                    }

                    fileVersion = header.ReplayVersion;
                }
            }

            // Old replays
            if (fileVersion <= PRE_REFACTOR_ENGINE_VERSION)
            {
                var value = V012ReplaySerializer.DeserializeReplay(data, fileVersion);

                replay = value.Replay;
                return value.Result;
            }
            else
            {
                var value = ReplaySerializer.DeserializeReplay(data, fileVersion);

                replay = value.Replay;
                return value.Result;
            }
        }

        // note: [NotNullWhen(ReplayReadResult.Valid)] is not a valid form of [NotNullWhen],
        // so replayFile will always be indicated as possibly being null
        // public static ReplayReadResult ReadReplay(string path, out ReplayNew? replay)
        // {
        //     using var stream = File.OpenRead(path);
        //     using var reader = new BinaryReader(stream);
        //
        //     try
        //     {
        //         replayFile = ReplayFile.Create(reader);
        //
        //         if (replayFile.Header.Magic != REPLAY_MAGIC_HEADER) return ReplayReadResult.NotAReplay;
        //
        //         var version = replayFile.Header.ReplayVersion;
        //         if (InvalidVersions.Contains(version) || version > REPLAY_VERSION) return ReplayReadResult.InvalidVersion;
        //
        //         replayFile.ReadData(reader, replayFile.Header.ReplayVersion);
        //
        //         return ReplayReadResult.Valid;
        //     }
        //     catch (Exception ex)
        //     {
        //         YargLogger.LogException(ex, "Failed to read replay file");
        //         replayFile = null;
        //         return ReplayReadResult.Corrupted;
        //     }
        // }
        //
        // public static HashWrapper? WriteReplay(string path, Replay replay)
        // {
        //     using var stream = File.OpenWrite(path);
        //     using var writer = new BinaryWriter(stream);
        //
        //     try
        //     {
        //         var replayFile = new ReplayFile(replay);
        //
        //         replayFile.Serialize(writer);
        //         return replayFile.Header.ReplayChecksum;
        //     }
        //     catch (Exception ex)
        //     {
        //         YargLogger.LogException(ex, "Failed to write replay file");
        //     }
        //
        //     return null;
        // }
    }
}