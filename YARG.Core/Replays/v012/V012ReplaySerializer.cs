using System.IO;

namespace YARG.Core.Replays
{
    public static partial class V012ReplaySerializer
    {
        #region Replay

        public static void SerializeReplay(BinaryWriter writer, ReplayNew replayNew)
        {
            Sections.SerializeHeader(writer, replayNew.Header);
            Sections.SerializeMetadata(writer, replayNew.Metadata);
            Sections.SerializePresetContainer(writer, replayNew.PresetContainer);

            writer.Write(replayNew.PlayerCount);
            foreach (var playerName in replayNew.PlayerNames)
            {
                writer.Write(playerName);
            }

            foreach (var frame in replayNew.Frames)
            {
                Sections.SerializeFrame(writer, frame);
            }
        }

        public static ReplayNew? DeserializeReplay(BinaryReader reader, int version = 0)
        {
            var replay = new ReplayNew();

            var header = Sections.DeserializeHeader(reader, version);
            if (header == null)
            {
                return null;
            }

            replay.Header = header.Value;

            replay.Metadata = Sections.DeserializeMetadata(reader, version);
            replay.PresetContainer = Sections.DeserializePresetContainer(reader, version);

            int playerCount = reader.ReadInt32();
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

            return replay;
        }

        #endregion
    }
}