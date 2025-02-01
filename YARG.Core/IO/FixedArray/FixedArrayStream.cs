using System;
using System.Runtime.CompilerServices;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public struct FixedArrayStream
    {
        private readonly unsafe byte* _data;
        private readonly long _length;
        private long _position;

        public long Position
        {
            readonly get { return _position; }
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                _position = value;
            }
        }

        public unsafe byte* PositionPointer
        {
            readonly get { return _data + _position; }
            set
            {
                if (value < _data || value > _data + _length)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                _position = value - _data;
            }
        }

        public readonly long Length => _length;

        public byte ReadByte()
        {
            if (_position >= _length)
            {
                throw new InvalidOperationException();
            }

            unsafe
            {
                return _data[_position++];
            }
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public T Read<T>(Endianness endianness)
            where T : unmanaged, IComparable, IComparable<T>, IConvertible, IEquatable<T>, IFormattable
        {
            unsafe
            {
                if (_position + sizeof(T) >_length)
                {
                    throw new InvalidOperationException();
                }

                var value = *(T*)(_data + _position);
                _position += sizeof(T);
                StreamExtensions.CorrectByteOrder(&value, endianness);
                return value;
            }
        }

        public unsafe void Read(void* pos, long count)
        {
            if (count < 0 || _position + count > _length)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            Unsafe.CopyBlockUnaligned(pos, _data + _position, (uint)count);
            _position += count;
        }

        public string ReadString()
        {
            int length = Read7BitEncodedInt();
            string str;
            unsafe
            {
                str = Encoding.UTF8.GetString(_data + _position, length); 
            }
            _position += length;
            return str;
        }

        public int Read7BitEncodedInt()
        {
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                byteReadJustNow = ReadByte();
                result |= (byteReadJustNow & 0x7Fu) << shift;
                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            byteReadJustNow = ReadByte();
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("LEB value exceeds max allowed");
            }

            result |= (uint) byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return (int) result;
        }

        public Guid ReadGuid()
        {
            unsafe
            {
                var bytes = stackalloc byte[16];
                Read(bytes, 16);
                return new Guid(new ReadOnlySpan<byte>(bytes, 16));
            }
        }

        public FixedArrayStream Slice(long length)
        {
            if (length < 0 || _position + length > _length)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            var slice = _position;
            _position += length;
            unsafe
            {
                return new FixedArrayStream(_data + slice, length);
            }
        }

        public FixedArrayStream(in FixedArray<byte> data)
        {
            unsafe
            {
                _data = data.Ptr; 
            }
            _length = data.Length;
            _position = 0;
        }

        private unsafe FixedArrayStream(byte* ptr, long length)
        {
            unsafe
            {
                _data = ptr;
            }
            _length = length;
            _position = 0;
        }
    }
}
