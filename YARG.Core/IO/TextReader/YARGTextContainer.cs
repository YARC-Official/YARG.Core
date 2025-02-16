using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.IO
{
    public struct YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
    {
        private readonly unsafe TChar* _data;
        private readonly long _length;
        private Encoding _encoding;
        private long _position;

        public long Position
        {
            readonly get { return _position; }
            set { _position = value; }
        }

        public Encoding Encoding
        {
            readonly get { return _encoding; }
            set { _encoding = value; }
        }

        public readonly long Length => _length;

        public readonly unsafe TChar* PositionPointer => _data + _position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe TChar* GetBuffer() { return _data; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<TChar> GetSpanOfRemainder()
        {
            unsafe
            {
                return new ReadOnlySpan<TChar>(_data + _position, (int) (_length - _position));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCurrentCharacter()
        {
            if (_position >= _length)
            {
                throw new InvalidOperationException();
            }

            unsafe
            {
                return _data[_position].ToInt32(null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int Get()
        {
            unsafe
            {
                return _data[_position].ToInt32(null);
            }
        }

        public readonly int this[long index]
        {
            get
            {
                unsafe
                {
                   return _data[_position + index].ToInt32(null);
                }
            }
        }

        public readonly int At(long index)
        {
            long pos = _position + index;
            if (pos < 0 || pos >= _length)
            {
                throw new InvalidOperationException();
            }

            unsafe
            {
                return _data[pos].ToInt32(null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsAtEnd() { return _position >= _length; }

        public YARGTextContainer(in FixedArray<TChar> data, Encoding encoding)
        {
            unsafe
            {
                _data = data.Ptr;
            }
            _length = data.Length;
            _encoding = encoding;
            _position = 0;
        }
    }
}
