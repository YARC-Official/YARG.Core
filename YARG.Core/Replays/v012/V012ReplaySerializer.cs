using System;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Extensions;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public static partial class V012ReplaySerializer
    {
        #region Replay

        public static void SerializeReplay(BinaryWriter writer, Replay replay)
        {
            writer.BaseStream.Seek(ReplayHeader.SIZE, SeekOrigin.Begin);

            Sections.SerializeMetadata(writer, replay.Metadata);
            Sections.SerializePresetContainer(writer, replay.PresetContainer);

            writer.Write(replay.PlayerCount);
            foreach (var playerName in replay.PlayerNames)
            {
                writer.Write(playerName);
            }

            foreach (var frame in replay.Frames)
            {
                Sections.SerializeFrame(writer, frame);
            }

            writer.BaseStream.Seek(ReplayHeader.SIZE, SeekOrigin.Begin);

            var hashWrapper = HashWrapper.Create(HashWrapper.Algorithm.ComputeHash(writer.BaseStream));
            replay.Header.ReplayChecksum = hashWrapper;

            writer.BaseStream.Seek(0, SeekOrigin.Begin);

            Sections.SerializeHeader(writer, replay.Header);
        }

        public static (ReplayReadResult Result, Replay? Replay) DeserializeReplay(UnmanagedMemoryStream stream, int version = 0)
        {
            var replay = new Replay();

            replay.Metadata = Sections.DeserializeMetadata(stream, version);
            replay.PresetContainer = Sections.DeserializePresetContainer(stream, version);

            int playerCount = stream.Read<int>(Endianness.Little);

            if (playerCount > 255)
            {
                return (ReplayReadResult.Corrupted, null);
            }

            var playerNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerNames[i] = stream.ReadString();
            }

            replay.Frames = new ReplayFrame[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                replay.Frames[i] = Sections.DeserializeFrame(stream, version);
            }

            return (ReplayReadResult.Valid, replay);
        }

        #endregion
    }
}