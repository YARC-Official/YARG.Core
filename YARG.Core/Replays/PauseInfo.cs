using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Replays
{
    public struct PauseInfo
    {
        public double PauseTime;
        public double PauseLength;

        public void Serialize(BinaryWriter stream)
        {
            stream.Write(PauseTime);
            stream.Write(PauseLength);
        }

        public PauseInfo(ref FixedArrayStream stream, int version)
        {
            PauseTime = stream.Read<double>(Endianness.Little);
            PauseLength = stream.Read<double>(Endianness.Little);
        }
    }
}