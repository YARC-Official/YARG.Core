using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly string Name;
        public readonly long Position;
        public readonly long Length;

        public SngFileListing(string name, BinaryReader reader)
        {
            Name = name;
            Length = reader.Read<long>(Endianness.Little);
            Position = reader.Read<long>(Endianness.Little);
        }

        public byte[] LoadAllBytes(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return SngFileStream.LoadFile(stream, sngFile.Mask.Clone(), Length, Position);
        }

        public SngFileStream CreateStream(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return new SngFileStream(Name, stream, sngFile.Mask.Clone(), Length, Position);
        }
    }
}
