using System;

namespace YARG.Core.Extensions
{
    public static class SpanExtensions
    {
        /// <summary>
        /// Removes up to a single leading and a single trailing occurrence
        /// of a specified character from a read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[0] == trimChar)
                buffer = buffer[1..];
            if (!buffer.IsEmpty && buffer[^1] == trimChar)
                buffer = buffer[..^1];

            return buffer;
        }

        /// <summary>
        /// Removes up to a single leading occurrence
        /// of a specified character from a read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimStartOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[0] == trimChar)
                buffer = buffer[1..];

            return buffer;
        }

        /// <summary>
        /// Removes up to a single trailing occurrence
        /// of a specified character from a read-only character span.
        /// </summary>
        public static ReadOnlySpan<char> TrimEndOnce(this ReadOnlySpan<char> buffer, char trimChar)
        {
            if (!buffer.IsEmpty && buffer[^1] == trimChar)
                buffer = buffer[..^1];

            return buffer;
        }
    }
}