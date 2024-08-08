using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public static partial class V012ReplaySerializer
    {
        #region Replay

        public static void SerializeReplay(BinaryWriter writer, Replay replay)
        {
            Sections.SerializeHeader(writer, replay.Header);
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
        }

        public static (ReplayReadResult Result, Replay? Replay) DeserializeReplay(byte[] data, int version = 0)
        {
            var reader = new SpanBinaryReader(data.AsSpan());

            var replay = new Replay();

            var header = Sections.DeserializeHeader(reader, version);
            if (header == null)
            {
                return (ReplayReadResult.NotAReplay, null);
            }

            replay.Header = header.Value;

            replay.Metadata = Sections.DeserializeMetadata(reader, version);
            replay.PresetContainer = Sections.DeserializePresetContainer(reader, version);

            int playerCount = reader.ReadInt32();

            if (playerCount > 255)
            {
                return (ReplayReadResult.Corrupted, null);
            }

            var playerNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerNames[i] = reader.ReadString();
            }

            replay.Frames = new ReplayFrame[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                replay.Frames[i] = Sections.DeserializeFrame(reader, version);
            }

            return (ReplayReadResult.Valid, replay);
        }

        #endregion
    }
}