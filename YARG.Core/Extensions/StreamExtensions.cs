using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.Extensions
{
    public enum Endianness
    {
        Little = 0,
        Big = 1,
    };

    public static class StreamExtensions
    {
        public static TType Read<TType>(this Stream stream, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            TType value = default;
            unsafe
            {
                if (stream.Read(new Span<byte>(&value, sizeof(TType))) != sizeof(TType))
                {
                    throw new EndOfStreamException($"Not enough data in the stream to read {typeof(TType)} ({sizeof(TType)} bytes)!");
                }
                CorrectByteOrder(&value, endianness);
            }
            return value;
        }

        public static bool ReadBoolean(this Stream stream)
        {
            byte b = (byte)stream.ReadByte();
            unsafe
            {
                return *(bool*)&b;
            }
        }

        public static int Read7BitEncodedInt(this Stream stream)
        {
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                byteReadJustNow = (byte) stream.ReadByte();
                result |= (byteReadJustNow & 0x7Fu) << shift;
                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            byteReadJustNow = (byte) stream.ReadByte();
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("LEB value exceeds max allowed");
            }

            result |= (uint) byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return (int) result;
        }

        public static string ReadString(this Stream stream, int length)
        {
            if (length == 0)
            {
                return string.Empty;
            }

            if (stream is UnmanagedMemoryStream unmanaged) unsafe
            {
                string str = Encoding.UTF8.GetString(unmanaged.PositionPointer, length);
                stream.Position += length;
                return str;
            }
            else if (stream is MemoryStream managed)
            {
                string str = Encoding.UTF8.GetString(managed.GetBuffer(), (int) managed.Position, length);
                stream.Position += length;
                return str;
            }

            var bytes = stream.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static string ReadString(this Stream stream)
        {
            int length = Read7BitEncodedInt(stream);

            return stream.ReadString(length);
        }

        public static string ReadString(this Stream stream, Endianness endianness)
        {
            uint length = stream.Read<uint>(endianness);

            if (length > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "String length is too long.");
            }

            return stream.ReadString((int) length);
        }

        public static Color ReadColor(this Stream stream)
        {
            int argb = stream.Read<int>(Endianness.Little);
            return Color.FromArgb(argb);
        }

        public static Guid ReadGuid(this Stream stream)
        {
            Span<byte> span = stackalloc byte[16];
            if (stream.Read(span) != span.Length)
            {
                throw new EndOfStreamException("Failed to read GUID, ran out of bytes!");
            }
            return new Guid(span);
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

        /// <summary>
        /// Reads bytes from the stream until a specific 4-byte barrier is found.
        /// It returns the bytes read *before* the barrier and advances the stream
        /// to the position *after* the barrier.
        /// </summary>
        public static ReadOnlySpan<byte> ReadUntilBarrier(this Stream stream, ReadOnlySpan<byte> barrier)
        {
            if (barrier.Length != 4)
            {
                throw new ArgumentException("Barrier must be 4 bytes long.", nameof(barrier));
            }

            if (stream is MemoryStream memoryStream)
            {
                var remaining = (int) (memoryStream.Length - memoryStream.Position);
                return SpanReadUntilBarrier(memoryStream.GetBuffer().AsSpan((int) memoryStream.Position, remaining), barrier,
                    stream);
            }

            if (stream is UnmanagedMemoryStream unmanagedStream)
            {
                unsafe
                {
                    return SpanReadUntilBarrier(
                        new ReadOnlySpan<byte>(unmanagedStream.PositionPointer,
                            (int) (unmanagedStream.Length - unmanagedStream.Position)), barrier,
                            stream);
                }
            }

            // Slow path, which should never be necessary, so should really probably go away entirely
            // Reads slow because of single byte reads, allocates too much memory, just bad
            using var memoryStream2 = new MemoryStream();
            byte[] buffer = new byte[4];
            while (stream.Read(buffer, 0, 1) > 0)
            {
                // Maybe we happened to match the first byte?
                if (buffer[0] == barrier[0])
                {
                    if (stream.Read(buffer, 1, 3) == 3 && barrier.SequenceEqual(buffer))
                    {
                        return memoryStream2.ToArray();
                    }

                    // No match, save read data and continue
                    memoryStream2.Write(buffer, 0, 4);
                    continue;
                }

                // No match, save read data and continue
                memoryStream2.Write(buffer, 0, 1);
            }

            throw new InvalidDataException("Could not find the specified barrier in the stream.");
        }

        private static ReadOnlySpan<byte> SpanReadUntilBarrier(ReadOnlySpan<byte> span, ReadOnlySpan<byte> barrier,
            Stream stream)
        {
            var index = span.IndexOf(barrier);
            if (index == -1)
            {
                throw new InvalidDataException("Could not find the specified barrier in the stream.");
            }

            // Position the stream to the end of the barrier
            stream.Position += index + barrier.Length;

            // Return the data up to the barrier
            return span[..index];
        }

        public static void Write<TType>(this Stream stream, TType value, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            unsafe
            {
                CorrectByteOrder(&value, endianness);
                stream.Write(new Span<byte>(&value, sizeof(TType)));
            }
        }

        public static void Write(this Stream stream, bool value)
        {
            unsafe
            {
                stream.WriteByte(*(byte*) &value);
            }
        }

        public static void Write7BitEncodedInt(this Stream stream, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint) value;   // support negative numbers
            while (v >= 0x80)
            {
                stream.WriteByte((byte) (v | 0x80));
                v >>= 7;
            }
            stream.WriteByte((byte) v);
        }

        public static unsafe void Write(this Stream stream, string value)
        {
            if (value.Length == 0)
            {
                stream.WriteByte(0);
                return;
            }

            var buffer = stackalloc byte[value.Length * 4];
            fixed (char* chars = value)
            {
                int len = Encoding.UTF8.GetBytes(chars, value.Length, buffer, value.Length * 4);
                stream.Write7BitEncodedInt(len);
                stream.Write(new ReadOnlySpan<byte>(buffer, len));
            }
        }

        public static void Write(this Stream stream, Color color)
        {
            stream.Write(color.ToArgb(), Endianness.Little);
        }

        public static void Write(this Stream stream, Guid guid)
        {
            Span<byte> span = stackalloc byte[16];
            if (!guid.TryWriteBytes(span))
            {
                throw new InvalidOperationException("Failed to write GUID bytes.");
            }
            stream.Write(span);
        }

        public static unsafe void CorrectByteOrder<TType>(TType* value, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            // Have to flip bits if the OS uses the opposite Endian
            if ((endianness == Endianness.Little) != BitConverter.IsLittleEndian)
            {
                int half = sizeof(TType) >> 1;
                byte* bytes = (byte*)value;
                for (int i = 0, j = sizeof(TType) - 1; i < half; ++i, --j)
                {
                    (bytes[j], bytes[i]) = (bytes[i], bytes[j]);
                }
            }
        }
    }
}
