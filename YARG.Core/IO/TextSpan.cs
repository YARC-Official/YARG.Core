using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.IO
{
    public unsafe struct TextSpan
    {
        public static readonly TextSpan Empty = new()
        {
            ptr = null,
            length = 0
        };

        public byte* ptr;
        public long length;

        public readonly bool IsEmpty => ptr == null || length == 0;

        public readonly ReadOnlySpan<byte> Span => new Span<byte>(ptr, (int)length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool SequenceEqual(in TextSpan other)
        {
            return Span.SequenceEqual(other.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly string GetString(Encoding encoding)
        {
            return encoding.GetString(ptr, (int)length);
        }
    }
}
