using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public enum Endianness
    {
        LittleEndian = 0,
        BigEndian = 1,
    };

#nullable enable
    public sealed class YARGBinaryReader
    {
        private readonly byte[] data;
        private readonly ReadOnlyMemory<byte> memory;
        private int _position;

        public int Length => memory.Length;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value > memory.Length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        public YARGBinaryReader(byte[] data)
        {
            this.data = data;
            memory = data;
        }

        public YARGBinaryReader(YARGBinaryReader baseReader, int length)
        {
            data = Array.Empty<byte>();
            memory = baseReader.memory.Slice(baseReader._position, length);
            baseReader._position += length;
        }

        public bool CompareTag(byte[] tag)
        {
            var span = memory.Span;
            Debug.Assert(tag.Length == 4);
            if (tag[0] != span[_position] ||
                tag[1] != span[_position + 1] ||
                tag[2] != span[_position + 2] ||
                tag[3] != span[_position + 3])
                return false;

            _position += 4;
            return true;
        }

        public void Move_Unsafe(int amount)
        {
            _position += amount;
        }

        public byte PeekByte()
        {
            return memory.Span[_position];
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
            var span = memory.Span.Slice(_position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt16BigEndian(span);
            _position += 2;
            return value;
        }
        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian)
        {
            ushort value;
            var span = memory.Span.Slice(_position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt16BigEndian(span);
            _position += 2;
            return value;
        }
        public int ReadInt32(Endianness endianness = Endianness.LittleEndian)
        {
            int value;
            var span = memory.Span.Slice(_position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt32BigEndian(span);
            _position += 4;
            return value;
        }
        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian)
        {
            uint value;
            var span = memory.Span.Slice(_position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt32BigEndian(span);
            _position += 4;
            return value;
        }
        public long ReadInt64(Endianness endianness = Endianness.LittleEndian)
        {
            long value;
            var span = memory.Span.Slice(_position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt64BigEndian(span);
            Position += 8;
            return value;
        }
        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian)
        {
            ulong value;
            var span = memory.Span.Slice(_position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += 8;
            return value;
        }
        public float ReadFloat()
        {
            float value = BitConverter.ToSingle(memory.Span.Slice(_position, 4));
            _position += 4;
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

        public uint ReadVLQ()
        {
            var span = memory.Span;
            uint value = 0;
            uint i = 0;
            while (true)
            {
                uint b = span[_position++];
                value |= b & 127;
                if (b < 128)
                    return value;

                if (i == 3)
                    throw new Exception("Invalid variable length quantity");

                value <<= 7;
                ++i;
            }
        }

        public ReadOnlySpan<byte> ReadSpan(int length)
        {
            int endPos = _position + length;
            var span = memory.Span.Slice(_position, length);
            _position = endPos;
            return span;
        }
    }
}
