using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public enum Endianness
    {
        LittleEndian = 0,
        BigEndian = 1,
    };

    public sealed class YARGBinaryReader
    {
        private readonly byte[] data;
        private readonly ReadOnlyMemory<byte> memory;
        private int _position;

        public YARGBinaryReader(byte[] data)
        {
            this.data = data;
            memory = data;
        }

        public YARGBinaryReader(Stream stream, int count)
        {
            if (stream is MemoryStream mem)
            {
                data = Array.Empty<byte>();
                memory = new ReadOnlyMemory<byte>(mem.GetBuffer(), (int) mem.Position, count);
                mem.Position += count;
            }
            else
            {
                data = stream.ReadBytes(count);
                memory = data;
            }
        }

        public void Move(int amount)
        {
            _position += amount;
            if (_position > memory.Length)
                throw new ArgumentOutOfRangeException("amount");
        }

        public byte ReadByte()
        {
            return memory.Span[_position++];
        }

        public sbyte ReadSByte()
        {
            return (sbyte) memory.Span[_position++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() > 0;
        }

        public short ReadInt16(Endianness endianness = Endianness.LittleEndian)
        {
            short value;
            var span = memory.Span.Slice(_position, sizeof(short));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt16BigEndian(span);
            _position += sizeof(short);
            return value;
        }

        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian)
        {
            ushort value;
            var span = memory.Span.Slice(_position, sizeof(ushort));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt16BigEndian(span);
            _position += sizeof(ushort);
            return value;
        }

        public int ReadInt32(Endianness endianness = Endianness.LittleEndian)
        {
            int value;
            var span = memory.Span.Slice(_position, sizeof(int));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt32BigEndian(span);
            _position += sizeof(int);
            return value;
        }

        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian)
        {
            uint value;
            var span = memory.Span.Slice(_position, sizeof(uint));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt32BigEndian(span);
            _position += sizeof(uint);
            return value;
        }

        public long ReadInt64(Endianness endianness = Endianness.LittleEndian)
        {
            long value;
            var span = memory.Span.Slice(_position, sizeof(long));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt64BigEndian(span);
            _position += sizeof(long);
            return value;
        }

        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian)
        {
            ulong value;
            var span = memory.Span.Slice(_position, sizeof(ulong));
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += sizeof(ulong);
            return value;
        }

        public float ReadFloat(Endianness endianness = Endianness.LittleEndian)
        {
            uint memory = ReadUInt32(endianness);
            float value = Unsafe.As<uint, float>(ref memory);
            return value;
        }

        public double ReadDouble(Endianness endianness = Endianness.LittleEndian)
        {
            ulong memory = ReadUInt64(endianness);
            double value = Unsafe.As<ulong, double>(ref memory);
            return value;
        }

        public bool ReadBytes(byte[] bytes)
        {
            int endPos = _position + bytes.Length;
            if (endPos > memory.Length)
                return false;

            unsafe
            {
                fixed (byte* dst = bytes, src = memory.Span)
                {
                    Unsafe.CopyBlock(dst, src + _position, (uint) bytes.Length);
                }
            }

            _position = endPos;
            return true;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] bytes = new byte[length];
            if (!ReadBytes(bytes))
                throw new Exception("Length of section exceeds bounds");
            return bytes;
        }

        public string ReadLEBString()
        {
            int length = ReadLEB();
            return length > 0 ? Encoding.UTF8.GetString(ReadSpan(length)) : string.Empty;
        }

        public int ReadLEB()
        {
            var span = memory.Span;
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                byteReadJustNow = span[_position++];
                result |= (byteReadJustNow & 0x7Fu) << shift;

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            byteReadJustNow = span[_position++];
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("LEB value exceeds max allowed");
            }

            result |= (uint) byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return (int) result;
        }

        public ReadOnlySpan<byte> ReadSpan(int length)
        {
            int endPos = _position + length;
            var span = memory.Span.Slice(_position, length);
            _position = endPos;
            return span;
        }

        public YARGBinaryReader Slice(int length)
        {
            var local = _position;
            Move(length);
            return new YARGBinaryReader(memory.Slice(local, length));
        }

        private YARGBinaryReader(ReadOnlyMemory<byte> memory)
        {
            data = Array.Empty<byte>();
            this.memory = memory;
        }
    }
}
