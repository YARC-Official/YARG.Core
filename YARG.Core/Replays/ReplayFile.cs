using System;
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

        public static ReplayFile Create(IBinaryDataReader reader)
        {
            var replayFile = new ReplayFile
            {
                Header = new ReplayHeader(),
            };

            replayFile.Header.Deserialize(reader);

            return replayFile;
        }

        public void ReadData(IBinaryDataReader reader, int version = 0)
        {
            Replay = new Replay();
            Replay.Deserialize(reader, version);
        }

        public void Serialize(IBinaryDataWriter writer)
        {
            using var dataStream = new MemoryStream();
            using var bDataWriter = new NullStringBinaryWriter(dataStream);
            using var dataWriter = new BinaryWriterWrapper(bDataWriter);

            Replay.Serialize(dataWriter);
            var data = new ReadOnlySpan<byte>(dataStream.GetBuffer(), 0, (int) dataStream.Length);
            Header.ReplayChecksum = HashWrapper.Hash(data);

            Header.Serialize(writer);
            writer.Write(data);
        }

        public void Deserialize(IBinaryDataReader reader, int version = 0)
        {
            Header = new ReplayHeader();
            Replay = new Replay();

            Header.Deserialize(reader, version);
            Replay.Deserialize(reader, version);
        }
    }
}