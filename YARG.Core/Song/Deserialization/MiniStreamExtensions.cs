using System.IO;
namespace YARG.Core.Song.Deserialization
{
    public static class MiniStreamExtensions
    {
        static readonly object intLock = new();
        static readonly byte[] integerBuffer = new byte[4];
        public static int ReadInt32LE(this Stream s)
        {
            lock (intLock)
            {
                s.Read(integerBuffer, 0, 4);
                return integerBuffer[3] << 24 | integerBuffer[2] << 16 | integerBuffer[1] << 8 | integerBuffer[0];
            }
        }

        public static int ReadInt32BE(this Stream s)
        {
            lock (intLock)
            {
                s.Read(integerBuffer, 0, 4);
                return integerBuffer[0] << 24 | integerBuffer[1] << 16 | integerBuffer[2] << 8 | integerBuffer[3];
            }
        }

        public static byte[] ReadBytes(this Stream s, int length)
        {
            byte[] buffer = new byte[length];
            s.Read(buffer, 0, length);
            return buffer;
        }
    }
}
