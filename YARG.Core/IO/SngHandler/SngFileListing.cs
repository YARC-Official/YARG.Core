using System.IO;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly long Position;
        public readonly long Length;

        public SngFileListing(YARGBinaryReader reader)
        {
            Length = reader.Read<long>(Endianness.Little);
            Position = reader.Read<long>(Endianness.Little);
        }
    }
}
