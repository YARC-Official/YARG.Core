using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
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
    public unsafe class YARGBinaryReader
    {
        private readonly YARGFile? file;
        private readonly byte* data;
        private readonly int length;
        private readonly int[] boundaries = new int[8];

        private int boundaryIndex = 0;
        private int currentBoundary;

        private int _position;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value > boundaries[boundaryIndex])
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }
        public int Boundary { get { return currentBoundary; } }

        public YARGBinaryReader(byte* data, int length)
        {
            this.data = data;
            this.length = length;
            currentBoundary = boundaries[0] = length;
        }

        public YARGBinaryReader(YARGFile file) : this(file.Data, file.Length)
        {
            this.file = file;
        }

        public YARGBinaryReader(byte[] data) : this(new YARGFile(data)) { }

        public YARGBinaryReader(string path) : this(new YARGFile(path)) { }

        public void ExitSection()
        {
            _position = currentBoundary;
            if (boundaryIndex == 0)
                throw new Exception("ayo wtf bro");
            currentBoundary = boundaries[--boundaryIndex];
        }

        public void EnterSection(int length)
        {
            int boundary = _position + length;
            if (boundary > boundaries[boundaryIndex])
                throw new Exception("Invalid length for section");
            if (boundaryIndex == 7)
                throw new Exception("Nested Buffer limit reached");
            currentBoundary = boundaries[++boundaryIndex] = boundary;
        }

        public bool CompareTag(byte[] tag)
        {
            Debug.Assert(tag.Length == 4);
            if (tag[0] != data[_position] ||
                tag[1] != data[_position + 1] ||
                tag[2] != data[_position + 2] ||
                tag[3] != data[_position + 3])
                return false;

            _position += 4;
            return true;
        }

        public bool Move(int amount)
        {
            if (_position + amount > currentBoundary)
                return false;

            _position += amount;
            return true;
        }

        public void Move_Unsafe(int amount)
        {
            _position += amount;
        }

        public byte PeekByte()
        {
            return data[_position];
        }

        public bool ReadByte(ref byte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = data[_position++];
            return true;
        }

        public bool ReadSByte(ref sbyte value)
        {
            if (_position >= currentBoundary)
                return false;

            value = (sbyte) data[_position++];
            return true;
        }

        public bool ReadInt16(ref short value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt16BigEndian(span);
            _position += 2;
            return true;
        }

        public bool ReadUInt16(ref ushort value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 2 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 2);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt16LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt16BigEndian(span);
            _position += 2;
            return true;
        }

        public bool ReadInt32(ref int value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt32BigEndian(span);
            _position += 4;
            return true;
        }

        public bool ReadUInt32(ref uint value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 4 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 4);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt32LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt32BigEndian(span);
            _position += 4;
            return true;
        }

        public bool ReadInt64(ref long value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadInt64BigEndian(span);
            Position += 8;
            return true;
        }

        public bool ReadUInt64(ref ulong value, Endianness endianness = Endianness.LittleEndian)
        {
            if (_position + 8 > currentBoundary)
                return false;

            Span<byte> span = new(data + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += 8;
            return true;
        }

        public bool ReadFloat(ref float value)
        {
            if (_position + 4 > currentBoundary)
                return false;

            value = BitConverter.ToSingle(new Span<byte>(data + _position, 4));
            _position += 4;
            return true;
        }
        public byte ReadByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return data[_position++];
        }

        public ref byte ReadByte_Ref()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");
            return ref data[_position++];
        }

        public sbyte ReadSByte()
        {
            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            return (sbyte) data[_position++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() > 0;
        }
        public short ReadInt16(Endianness endianness = Endianness.LittleEndian)
        {
            short value = default;
            if (!ReadInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian)
        {
            ushort value = default;
            if (!ReadUInt16(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public int ReadInt32(Endianness endianness = Endianness.LittleEndian)
        {
            int value = default;
            if (!ReadInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian)
        {
            uint value = default;
            if (!ReadUInt32(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public long ReadInt64(Endianness endianness = Endianness.LittleEndian)
        {
            long value = default;
            if (!ReadInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian)
        {
            ulong value = default;
            if (!ReadUInt64(ref value, endianness))
                throw new Exception("Failed to parse data");
            return value;
        }
        public float ReadFloat()
        {
            float value = default;
            if (!ReadFloat(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }
        public bool ReadBytes(byte[] bytes)
        {
            int endPos = _position + bytes.Length;
            if (endPos > currentBoundary)
                return false;

            Marshal.Copy((IntPtr) (data + _position), bytes, 0, bytes.Length);
            _position = endPos;
            return true;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] bytes = new byte[length];
            if (!ReadBytes(bytes))
                throw new Exception("Failed to parse data");
            return bytes;
        }

        public string ReadLEBString()
        {
            int length = ReadLEB();
            return length > 0 ? Encoding.UTF8.GetString(ReadSpan(length)) : string.Empty;
        }

        public int ReadLEB()
        {
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                if (_position >= currentBoundary)
                    throw new Exception("Failed to parse data");

                byteReadJustNow = data[_position++];
                result |= (byteReadJustNow & 0x7Fu) << shift;

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            if (_position >= currentBoundary)
                throw new Exception("Failed to parse data");

            byteReadJustNow = data[_position++];
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("Failed to parse data");
            }

            result |= (uint) byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return (int) result;
        }

        public uint ReadVLQ()
        {
            uint value = 0;
            uint i = 0;
            while (true)
            {
                if (_position >= currentBoundary)
                    throw new Exception("Failed to parse data");

                uint b = data[_position++];
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
            if (endPos > currentBoundary)
                throw new Exception("Failed to parse data");

            ReadOnlySpan<byte> span = new(data + _position, length);
            _position = endPos;
            return span;
        }

        public YARGBinaryReader CreateReaderFromCurrentPosition(int length)
        {
            int endPos = _position + length;
            if (endPos > currentBoundary)
                throw new Exception("Failed to create reader");

            YARGBinaryReader reader = new(data + _position, length);
            _position = endPos;
            return reader;
        }
    }
}
