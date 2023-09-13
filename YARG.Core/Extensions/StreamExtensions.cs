using System;
using System.IO;

namespace YARG.Core.Extensions
{
    public static class StreamExtensions
    {
        public static int ReadInt32LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[4];
            s.Read(buffer);
            return buffer[3] << 24 | buffer[2] << 16 | buffer[1] << 8 | buffer[0];
        }

        public static int ReadInt32BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[4];
            s.Read(buffer);
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
