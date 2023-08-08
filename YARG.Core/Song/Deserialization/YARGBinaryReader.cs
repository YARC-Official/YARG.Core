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
    public sealed unsafe class YARGBinaryReader
    {
        private GCHandle handle;
        private readonly byte[] data;
        private readonly byte* ptr;
        private readonly int length;
        private int _position;

        public int Length => length;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value > length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        public YARGBinaryReader(byte[] data)
        {
            this.data = data;
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            ptr = (byte*)handle.AddrOfPinnedObject();
            length = data.Length;
        }

        public YARGBinaryReader(YARGBinaryReader baseReader, int length)
        {
            data = Array.Empty<byte>();
            ptr = baseReader.ptr + baseReader._position;
            this.length = length;
            baseReader._position += length;
        }

        ~YARGBinaryReader()
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        public bool CompareTag(byte[] tag)
        {
            Debug.Assert(tag.Length == 4);
            if (tag[0] != ptr[_position] ||
                tag[1] != ptr[_position + 1] ||
                tag[2] != ptr[_position + 2] ||
                tag[3] != ptr[_position + 3])
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
            return ptr[_position];
        }

        public byte ReadByte()
        {
            return ptr[_position++];
        }

        public sbyte ReadSByte()
        {
            return (sbyte) ptr[_position++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() > 0;
        }
        public short ReadInt16(Endianness endianness = Endianness.LittleEndian)
        {
            short value;
            ReadOnlySpan<byte> span = new(ptr + _position, 2);
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
            ReadOnlySpan<byte> span = new(ptr + _position, 2);
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
            ReadOnlySpan<byte> span = new(ptr + _position, 4);
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
            ReadOnlySpan<byte> span = new(ptr + _position, 4);
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
            ReadOnlySpan<byte> span = new(ptr + _position, 8);
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
            ReadOnlySpan<byte> span = new(ptr + _position, 8);
            if (endianness == Endianness.LittleEndian)
                value = BinaryPrimitives.ReadUInt64LittleEndian(span);
            else
                value = BinaryPrimitives.ReadUInt64BigEndian(span);
            _position += 8;
            return value;
        }
        public float ReadFloat()
        {
            float value = BitConverter.ToSingle(new ReadOnlySpan<byte>(ptr + _position, 4));
            _position += 4;
            return value;
        }
        public bool ReadBytes(byte[] bytes)
        {
            int endPos = _position + bytes.Length;
            if (endPos > length)
                return false;

            fixed (byte* dst = bytes)
            {
                Unsafe.CopyBlock(dst, ptr + _position, (uint) bytes.Length);
            }

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
                byteReadJustNow = ptr[_position++];
                result |= (byteReadJustNow & 0x7Fu) << shift;

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            byteReadJustNow = ptr[_position++];
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
                uint b = ptr[_position++];
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
            ReadOnlySpan<byte> span = new(ptr + _position, length);
            _position = endPos;
            return span;
        }
    }
}
