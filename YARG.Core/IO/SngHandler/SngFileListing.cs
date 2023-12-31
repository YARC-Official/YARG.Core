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
            var stream = CloneStream_Internal(file);
            return SngFileStream.LoadFile(stream, file.Mask.Clone(), Length, Position);
        }

        public SngFileStream CreateStream(SngFile file)
        {
            var stream = CloneStream_Internal(file);
            return new SngFileStream(stream, file.Mask.Clone(), Length, Position);
        }

        private Stream CloneStream_Internal(SngFile file)
        {
            if (file.Stream is YARGSongFileStream yargSongStream)
            {
                return yargSongStream.Clone();
            }

            var fs = file.Stream as FileStream;
            return new FileStream(fs!.Name, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }
    }
}
