using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.IO
{
    public struct YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
    {
        private readonly unsafe TChar* _data;

        public int Length { get; }

        public int Position { get; set; }

        public Encoding Encoding { get; set; }

        public readonly unsafe TChar* PositionPointer => _data + Position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe TChar* GetBuffer() { return _data; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<TChar> GetSpanOfRemainder()
        {
            unsafe
            {
                return new ReadOnlySpan<TChar>(_data + Position, Length - Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetCurrentCharacter()
        {
            if (Position >= Length)
            {
                throw new InvalidOperationException();
            }

            unsafe
            {
                return _data[Position].ToInt32(null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int Get()
        {
            unsafe
            {
                return _data[Position].ToInt32(null);
            }
        }

        public readonly int this[int index]
        {
            get
            {
                unsafe
                {
                   return _data[Position + index].ToInt32(null);
                }
            }
        }

        public readonly int At(int index)
        {
            int pos = Position + index;
            if (pos < 0 || pos >= Length)
            {
                throw new InvalidOperationException();
            }

            unsafe
            {
                return _data[pos].ToInt32(null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsAtEnd() { return Position >= Length; }

        public YARGTextContainer(FixedArray<TChar> data, Encoding encoding)
        {
            unsafe
            {
                _data = data.Ptr;
            }
            Length = data.Length;
            Encoding = encoding;
            Position = 0;
        }
    }
}
