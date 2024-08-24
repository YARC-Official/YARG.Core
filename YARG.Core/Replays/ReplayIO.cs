using System;
using System.IO;
using System.Linq;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;
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

        public const short REPLAY_VERSION = 6;
        public const short ENGINE_VERSION = 1;

        private const short PRE_REFACTOR_ENGINE_VERSION = 5;

        // Some versions may be invalidated (such as significant format changes)
        private static readonly int[] InvalidVersions =
        {
            0, 1, 2, 3
        };

        public static HashWrapper? WriteReplay(string path, Replay replay)
        {
            using var stream = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite);
            using var writer = new BinaryWriter(stream);

            try
            {
                replay.Header = new ReplayHeader
                {
                    Magic = REPLAY_MAGIC_HEADER,
                    ReplayVersion = REPLAY_VERSION,
                    EngineVersion = ENGINE_VERSION
                };
                ReplaySerializer.SerializeReplay(writer, replay);
                return replay.Header.ReplayChecksum;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to write replay file");
            }

            return null;
        }

        public static ReplayReadResult ReadReplay(string path, out Replay? replay)
        {
            replay = null;
            if (!File.Exists(path))
            {
                return ReplayReadResult.NotAReplay;
            }

            try
            {
                using var fileStream = File.OpenRead(path);

                if (!REPLAY_MAGIC_HEADER.Matches(fileStream))
                {
                    return ReplayReadResult.NotAReplay;
                }

                int replayVersion = fileStream.Read<int>(Endianness.Little);

                if (InvalidVersions.Contains(replayVersion) || replayVersion > REPLAY_VERSION)
                {
                    return ReplayReadResult.InvalidVersion;
                }

                int engineVersion = fileStream.Read<int>(Endianness.Little);
                var hash = HashWrapper.Deserialize(fileStream);

                var header = new ReplayHeader
                {
                    Magic = REPLAY_MAGIC_HEADER,
                    ReplayVersion = replayVersion,
                    EngineVersion = engineVersion,
                    ReplayChecksum = hash
                };

                var dataLength = fileStream.Length - fileStream.Position;

                using var data = AllocatedArray<byte>.Read(fileStream, dataLength);
                using var memoryStream = data.ToStream();

                var checksum = HashWrapper.Hash(data.ReadOnlySpan);

                if (!checksum.Equals(hash))
                {
                    return ReplayReadResult.Corrupted;
                }

                // Old replays
                if (replayVersion <= PRE_REFACTOR_ENGINE_VERSION)
                {
                    var value = V012ReplaySerializer.DeserializeReplay(memoryStream, replayVersion);

                    replay = value.Replay;
                    return value.Result;
                }
                else
                {
                    var value = ReplaySerializer.DeserializeReplay(memoryStream, replayVersion);

                    replay = value.Replay;
                    return value.Result;
                }
            } catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read replay file");
                return ReplayReadResult.Corrupted;
            }
        }
    }
}