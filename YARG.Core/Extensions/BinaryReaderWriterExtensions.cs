using System;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;

namespace YARG.Core.Extensions
{
    public static class BinaryReaderExtensions
    {
        public static Color ReadColor(this BinaryReader reader)
        {
            int argb = reader.ReadInt32();
            return Color.FromArgb(argb);
        }

        public static Guid ReadGuid(this BinaryReader reader)
        {
            Span<byte> span = stackalloc byte[16];
            if (reader.Read(span) != span.Length)
            {
                throw new EndOfStreamException("Failed to read GUID, ran out of bytes!");
            }

            return new Guid(span);
        }

        public static uint ReadUInt32BE(this BinaryReader reader)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        }

        public static ushort ReadUInt16BE(this BinaryReader reader)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
        }

        public static float ReadSingleBE(this BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Reads a string prefixed with a 32-bit big-endian length.
        /// </summary>
        public static byte[] ReadStringBE(this BinaryReader reader)
        {
            var length = reader.ReadUInt32BE();
            return reader.ReadBytes((int) length);
        }

        /// <summary>
        /// Reads bytes from the stream until a specific 4-byte barrier is found.
        /// It returns the bytes read *before* the barrier and advances the stream
        /// to the position *after* the barrier.
        /// </summary>
        public static ReadOnlySpan<byte> ReadUntilBarrier(this BinaryReader reader, ReadOnlySpan<byte> barrier)
        {
            if (barrier.Length != 4)
            {
                throw new ArgumentException("Barrier must be 4 bytes long.", nameof(barrier));
            }

            if (reader.BaseStream is MemoryStream memoryStream)
            {
                var remaining = (int) (memoryStream.Length - memoryStream.Position);
                return SpanReadUntilBarrier(memoryStream.GetBuffer().AsSpan((int) memoryStream.Position, remaining), barrier,
                    reader.BaseStream);
            }

            if (reader.BaseStream is UnmanagedMemoryStream unmanagedStream)
            {
                unsafe
                {
                    return SpanReadUntilBarrier(
                        new ReadOnlySpan<byte>(unmanagedStream.PositionPointer,
                            (int) (unmanagedStream.Length - unmanagedStream.Position)), barrier,
                            reader.BaseStream);
                }
            }

            // Slow path, which should never be necessary, so should really probably go away entirely
            // Reads slow because of single byte reads, allocates too much memory, just bad
            using var memoryStream2 = new MemoryStream();
            byte[] buffer = new byte[4];
            while (reader.Read(buffer, 0, 1) > 0)
            {
                // Maybe we happened to match the first byte?
                if (buffer[0] == barrier[0])
                {
                    if (reader.Read(buffer, 1, 3) == 3 && barrier.SequenceEqual(buffer))
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
            return span.Slice(0, index);
        }
    }

    public static class BinaryWriterExtensions
    {
        public static void Write(this BinaryWriter writer, Color color)
        {
            writer.Write(color.ToArgb());
        }

        public static void Write(this BinaryWriter writer, Guid guid)
        {
            Span<byte> span = stackalloc byte[16];
            if (!guid.TryWriteBytes(span))
            {
                throw new InvalidOperationException("Failed to write GUID bytes.");
            }

            writer.Write(span);
        }
    }
}