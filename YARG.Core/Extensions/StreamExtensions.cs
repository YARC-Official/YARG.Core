using System;
using System.Buffers.Binary;
using System.IO;

namespace YARG.Core.Extensions
{
    public static class StreamExtensions
    {
        #region Stream
        public static short ReadInt16LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (stream.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        public static short ReadInt16BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (stream.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public static ushort ReadUInt16LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (stream.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        public static ushort ReadUInt16BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (stream.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public static int ReadInt32LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (stream.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        public static int ReadInt32BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (stream.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public static uint ReadUInt32LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (stream.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        public static uint ReadUInt32BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (stream.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public static long ReadInt64LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (stream.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        public static long ReadInt64BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (stream.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        public static ulong ReadUInt64LE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (stream.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        public static ulong ReadUInt64BE(this Stream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (stream.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            if (stream.Read(buffer, 0, length) != length)
                throw new EndOfStreamException($"Not enough data in the buffer to read {length} bytes!");

            return buffer;
        }

        public static void WriteInt16LE(this Stream stream, short value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteInt16BE(this Stream stream, short value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt16LE(this Stream stream, ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt16BE(this Stream stream, ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteInt32LE(this Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteInt32BE(this Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt32LE(this Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt32BE(this Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteInt64LE(this Stream stream, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteInt64BE(this Stream stream, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt64LE(this Stream stream, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        public static void WriteUInt64BE(this Stream stream, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            stream.Write(buffer);
        }
        #endregion

        #region BinaryReader
        public static short ReadInt16LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (reader.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        public static short ReadInt16BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            if (reader.Read(buffer) != sizeof(short))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int16!");

            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public static ushort ReadUInt16LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (reader.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        public static ushort ReadUInt16BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            if (reader.Read(buffer) != sizeof(ushort))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt16!");

            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public static int ReadInt32LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (reader.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        public static int ReadInt32BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            if (reader.Read(buffer) != sizeof(int))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int32!");

            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public static uint ReadUInt32LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (reader.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        public static uint ReadUInt32BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            if (reader.Read(buffer) != sizeof(uint))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt32!");

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public static long ReadInt64LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (reader.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        public static long ReadInt64BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            if (reader.Read(buffer) != sizeof(long))
                throw new EndOfStreamException("Not enough data in the buffer to read an Int64!");

            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        public static ulong ReadUInt64LE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (reader.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        public static ulong ReadUInt64BE(this BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            if (reader.Read(buffer) != sizeof(ulong))
                throw new EndOfStreamException("Not enough data in the buffer to read a UInt64!");

            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }
        #endregion

        #region BinaryWriter
        public static void WriteInt16LE(this BinaryWriter writer, short value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteInt16BE(this BinaryWriter writer, short value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt16LE(this BinaryWriter writer, ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt16BE(this BinaryWriter writer, ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteInt32LE(this BinaryWriter writer, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteInt32BE(this BinaryWriter writer, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt32LE(this BinaryWriter writer, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt32BE(this BinaryWriter writer, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteInt64LE(this BinaryWriter writer, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteInt64BE(this BinaryWriter writer, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt64LE(this BinaryWriter writer, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        public static void WriteUInt64BE(this BinaryWriter writer, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            writer.Write(buffer);
        }
        #endregion
    }
}
