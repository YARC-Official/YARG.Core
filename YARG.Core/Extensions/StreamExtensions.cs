using System;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Extensions
{
    public static class StreamExtensions
    {
        public static TType Read<TType>(this Stream stream, Endianness endianness = Endianness.Little)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            TType value = default;
            unsafe
            {
                byte* buffer = (byte*)&value;
                if (stream.Read(new Span<byte>(buffer, sizeof(TType))) != sizeof(TType))
                {
                    throw new EndOfStreamException($"Not enough data in the stream to read {typeof(TType)} ({sizeof(TType)} bytes)!");
                }
                CorrectByteOrder<TType>(buffer, endianness);
            }
            return value;
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            if (stream.Read(buffer, 0, length) != length)
            {
                throw new EndOfStreamException($"Not enough data in the stream to read {length} bytes!");
            }
            return buffer;
        }

        public static void Write<TType>(this Stream stream, TType value, Endianness endianness = Endianness.Little)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            unsafe
            {
                byte* buffer = (byte*) &value;
                CorrectByteOrder<TType>(buffer, endianness);
                stream.Write(new Span<byte>(buffer, sizeof(TType)));
            }
        }

        private static unsafe void CorrectByteOrder<TType>(byte* bytes, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            // Have to flip bits if the OS uses the opposite Endian
            if ((endianness == Endianness.Little) != BitConverter.IsLittleEndian)
            {
                int half = sizeof(TType) >> 1;
                for (int i = 0, j = sizeof(TType) - 1; i < half; ++i, --j)
                    (bytes[j], bytes[i]) = (bytes[i], bytes[j]);
            }
        }
    }
}
