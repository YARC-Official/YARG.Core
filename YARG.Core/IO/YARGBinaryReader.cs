using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public enum Endianness
    {
        Little = 0,
        Big = 1,
    };

    public sealed class YARGBinaryReader
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _position;

        public YARGBinaryReader(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public YARGBinaryReader(Stream stream, int count)
        {
            if (stream is MemoryStream mem)
            {
                _data = new ReadOnlyMemory<byte>(mem.GetBuffer(), (int) mem.Position, count);
                mem.Position += count;
            }
            else
            {
                _data = stream.ReadBytes(count);
            }
        }

        public YARGBinaryReader Slice(int length)
        {
            var local = _position;
            Move(length);
            return new YARGBinaryReader(_data.Slice(local, length));
        }

        public void Move(int amount)
        {
            _position += amount;
            if (_position > _data.Length)
                throw new ArgumentOutOfRangeException("amount");
        }

        public byte ReadByte()
        {
            return _data.Span[_position++];
        }

        public sbyte ReadSByte()
        {
            return (sbyte) _data.Span[_position++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() > 0;
        }

        public TType Read<TType>(Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            unsafe
            {
                long pos = _position;
                // Will throw if invalid
                Move(sizeof(TType));

                fixed (byte* buf = _data.Span)
                {
                    // If the memory layout of the host system matches the layout of
                    // the value to be parsed from the file, we only require a cast
                    if ((endianness == Endianness.Little) == BitConverter.IsLittleEndian)
                        return *(TType*) (buf + pos);

                    // Reminder: _position moved
                    pos = _position;

                    // Otherwise, we have to flip the bytes
                    TType value = default;
                    byte* bytes = (byte*)&value;
                    for (int i = 0; i < sizeof(TType); ++i)
                        bytes[i] = buf[--pos];
                    return value;
                }
            }
        }

        public bool ReadBytes(Span<byte> bytes)
        {
            int endPos = _position + bytes.Length;
            if (endPos > _data.Length)
                return false;

            _data.Span.Slice(_position, bytes.Length).CopyTo(bytes);
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
            var span = _data.Span;
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
            var span = _data.Span.Slice(_position, length);
            _position = endPos;
            return span;
        }
    }
}
