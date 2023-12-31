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

        public byte[] LoadAllBytes(SngFile file)
        {
            return SngFileStream.LoadFile(file.Stream.Clone(), file.Mask.Clone(), Length, Position);
        }

        public SngFileStream CreateStream(SngFile file)
        {
            return new SngFileStream(file.Stream.Clone(), file.Mask.Clone(), Length, Position);
        }
    }
}
