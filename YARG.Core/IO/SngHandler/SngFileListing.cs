using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO.Disposables;

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
            Length = reader.ReadInt64();
            Position = reader.ReadInt64();
        }

        public AllocatedArray<byte> LoadAllBytes(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return SngFileStream.LoadFile(stream, sngFile.Mask, Length, Position);
        }

        public SngFileStream CreateStream(SngFile sngFile)
        {
            var stream = sngFile.LoadFileStream();
            return new SngFileStream(Name, stream, sngFile.Mask, Length, Position);
        }
    }
}
