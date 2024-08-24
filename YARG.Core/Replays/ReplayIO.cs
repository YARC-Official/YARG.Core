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
                byte[] data = File.ReadAllBytes(path);

                int fileVersion;

                unsafe
                {
                    fixed (byte* ptr = data)
                    {
                        // This is technically invalid for v0.12 replays as the header size was changed
                        // But because the engine version never changed from 0 it doesn't actually matter LOL
                        var header = Unsafe.Read<ReplayHeader>(ptr);

                        header.Magic = new EightCC(data);

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
            } catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read replay file");
                return ReplayReadResult.Corrupted;
            }
        }
    }
}