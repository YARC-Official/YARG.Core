using System;
using System.IO;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayFile : IBinarySerializable
    {
        public void Serialize(BinaryWriter writer)
        {
            using var dataStream = new MemoryStream();
            using var dataWriter = new NullStringBinaryWriter(dataStream);

            //Replay.Serialize(dataWriter);
            var data = new ReadOnlySpan<byte>(dataStream.GetBuffer(), 0, (int) dataStream.Length);
            //Header.ReplayChecksum = HashWrapper.Hash(data);

            //Header.Serialize(writer);
            writer.Write(data);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            //Header = new ReplayHeader();
            //Replay = new Replay();

            //Header.Deserialize(reader, version);
            //Replay.Deserialize(reader, version);
        }
    }
}