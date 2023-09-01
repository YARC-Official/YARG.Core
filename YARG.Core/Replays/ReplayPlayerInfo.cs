using System.IO;
using YARG.Core.Game;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public struct ReplayPlayerInfo : IBinarySerializable
    {
        public int         PlayerId;
        public int         ColorProfileId;
        public YargProfile Profile;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(ColorProfileId);

            Profile.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            PlayerId = reader.ReadInt32();
            ColorProfileId = reader.ReadInt32();

            Profile = new YargProfile();
            Profile.Deserialize(reader, version);
        }
    }
}