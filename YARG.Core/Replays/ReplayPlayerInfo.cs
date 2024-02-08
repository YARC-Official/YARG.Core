using System.Diagnostics.CodeAnalysis;
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

        public ReplayPlayerInfo(IBinaryDataReader reader, int version = 0) : this()
        {
            Deserialize(reader, version);
        }

        public void Serialize(IBinaryDataWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(ColorProfileId);

            Profile.Serialize(writer);
        }

        [MemberNotNull(nameof(Profile))]
        public void Deserialize(IBinaryDataReader reader, int version = 0)
        {
            PlayerId = reader.ReadInt32();
            ColorProfileId = reader.ReadInt32();

            Profile = new YargProfile();
            Profile.Deserialize(reader, version);
        }
    }
}