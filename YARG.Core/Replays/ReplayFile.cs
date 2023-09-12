using System.IO;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayFile : IBinarySerializable
    {
        public ReplayHeader Header { get; private set; }

        public Replay Replay { get; private set; }

        private ReplayFile()
        {
            Header = new ReplayHeader();
            Replay = new Replay();
        }

        public ReplayFile(Replay replay)
        {
            Header = new ReplayHeader
            {
                Magic = ReplayIO.REPLAY_MAGIC_HEADER,
                ReplayVersion = ReplayIO.REPLAY_VERSION,
            };

            Replay = replay;
        }

        public static ReplayFile Create(BinaryReader reader)
        {
            var replayFile = new ReplayFile
            {
                Header = new ReplayHeader(),
            };

            replayFile.Header.Deserialize(reader);

            return replayFile;
        }

        public void ReadData(BinaryReader reader, int version = 0)
        {
            Replay = new Replay();
            Replay.Deserialize(reader, version);
        }

        public void Serialize(BinaryWriter writer)
        {
            using var dataStream = new MemoryStream();
            using var dataWriter = new NullStringBinaryWriter(dataStream);

            Replay.Serialize(dataWriter);
            Header.ReplayChecksum = HashWrapper.Create(dataStream);

            Header.Serialize(writer);
            dataStream.CopyTo(writer.BaseStream);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            Header = new ReplayHeader();
            Replay = new Replay();

            Header.Deserialize(reader, version);
            Replay.Deserialize(reader, version);
        }
    }
}