using System;
using System.IO;
using YARG.Core.IO;

namespace YARG.Core.Extensions
{
    public static class StreamExtensions
    {
        public static TType Read<TType>(this Stream stream, Endianness endianness = Endianness.LittleEndian)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            TType value = default;
            unsafe
            {
                byte* buffer = (byte*)&value;
                if (stream.Read(new Span<byte>(buffer, sizeof(TType))) != sizeof(TType))
                    throw new EndOfStreamException($"Not enough data in the stream to read {typeof(TType)} ({sizeof(TType)} bytes)!");

                // Have to flip bits if the OS uses the opposite Endian
                if ((endianness == Endianness.LittleEndian) != BitConverter.IsLittleEndian)
                {
                    int half = sizeof(TType) >> 1;
                    for (int i = 0, j = sizeof(TType) - 1; i < half; ++i, --j)
                        (buffer[j], buffer[i]) = (buffer[i], buffer[j]);
                }
            }
            return value;
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            if (stream.Read(buffer, 0, length) != length)
                throw new EndOfStreamException($"Not enough data in the stream to read {length} bytes!");

            return buffer;
        }

        public static void Write<TType>(this Stream stream, TType value, Endianness endianness = Endianness.LittleEndian)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            unsafe
            {
                byte* buffer = (byte*) &value;
                // Have to flip bits if the OS uses the opposite Endian
                if ((endianness == Endianness.LittleEndian) != BitConverter.IsLittleEndian)
                {
                    int half = sizeof(TType) >> 1;
                    for (int i = 0, j = sizeof(TType) - 1; i < half; ++i, --j)
                        (buffer[j], buffer[i]) = (buffer[i], buffer[j]);
                }
                stream.Write(new Span<byte>(buffer, sizeof(TType)));
            }
        }
    }
}
