using System;
using System.Buffers.Binary;
using System.IO;

namespace YARG.Core.Extensions
{
    public static class StreamExtensions
    {
        public static short ReadInt16LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (s.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        public static short ReadInt16BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (s.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public static ushort ReadUInt16LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (s.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        public static ushort ReadUInt16BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (s.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public static int ReadInt32LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (s.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        public static int ReadInt32BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (s.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public static uint ReadUInt32LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (s.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        public static uint ReadUInt32BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (s.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public static long ReadInt64LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (s.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        public static long ReadInt64BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (s.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        public static ulong ReadUInt64LE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (s.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        public static ulong ReadUInt64BE(this Stream s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (s.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        public static byte[] ReadBytes(this Stream s, int length)
        {
            byte[] buffer = new byte[length];
            s.Read(buffer, 0, length);
            return buffer;
        }
    }
}
