using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Game;

namespace YARG.Core.Replays
{
    public readonly struct ReplayPlayerInfo
    {
        public readonly int         PlayerId;
        public readonly int         ColorProfileId;
        public readonly YargProfile Profile;

        public ReplayPlayerInfo(int playerid, int colorProfileId, YargProfile profile)
        {
            PlayerId = playerid;
            ColorProfileId = colorProfileId;
            Profile = profile;
        }

        public ReplayPlayerInfo(Stream stream)
        {
            PlayerId = stream.Read<int>(Endianness.Little);
            ColorProfileId = stream.Read<int>(Endianness.Little);
            Profile = new YargProfile(stream);
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(ColorProfileId);

            Profile.Serialize(writer);
        }
    }
}