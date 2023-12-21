using System.IO;

namespace YARG.Core.IO
{
    public class SngFileListing
    {
        public readonly long Position;
        public readonly long Length;

        public readonly bool IsYARGSong;

        public SngFileListing(YARGBinaryReader reader, bool isYARGSong)
        {
            Length = reader.Read<long>(Endianness.Little);
            Position = reader.Read<long>(Endianness.Little);

            IsYARGSong = isYARGSong;
        }

        public byte[] LoadAllBytes(string filename, SngMask mask)
        {
            var stream = CreateStreamInternal(filename);
            return SngFileStream.LoadFile(stream, Length, Position, mask.Clone());
        }

        public SngFileStream CreateStream(string filename, SngMask mask)
        {
            var stream = CreateStreamInternal(filename);
            return new SngFileStream(stream, Length, Position, mask.Clone());
        }

        private Stream CreateStreamInternal(string path)
        {
            if (IsYARGSong)
            {
                return new YARGSongFileStream(path);
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }
    }
}