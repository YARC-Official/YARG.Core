using System.IO;
namespace YARG.Core.Song.Deserialization
{
    public static class MiniStreamExtensions
    {
        public static int ReadInt32LE(this Stream s)
        {
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return buffer[3] << 24 | buffer[2] << 16 | buffer[1] << 8 | buffer[0];
        }

        public static int ReadInt32BE(this Stream s)
        {
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];
        }

        public static byte[] ReadBytes(this Stream s, int length)
        {
            byte[] buffer = new byte[length];
            s.Read(buffer, 0, length);
            return buffer;
        }
    }
}
