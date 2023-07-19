using System;

namespace YARG.Core.Utility
{
    /// <summary>
    /// Enumerates a <see cref="ReadOnlySpan{T}"/>, splitting based on a specific value of <typeparamref name="T"/>.
    /// </summary>
    public ref struct SpanSplitter<T>
        where T : IEquatable<T>
    {
        private readonly ReadOnlySpan<T> _original;
        private ReadOnlySpan<T> _remaining;
        private readonly T _split;

        public ReadOnlySpan<T> Current { get; private set; }

        public readonly ReadOnlySpan<T> Original => _original;
        public readonly ReadOnlySpan<T> Remaining => _remaining;

        public SpanSplitter(ReadOnlySpan<T> buffer, T split)
        {
            _original = buffer;
            _remaining = buffer;
            _split = split;
            Current = ReadOnlySpan<T>.Empty;
        }

        public readonly SpanSplitter<T> GetEnumerator() => this;

        public ReadOnlySpan<T> GetNext() => MoveNext() ? Current : ReadOnlySpan<T>.Empty;

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            // Find next non-empty segment
            int splitIndex = 0;
            while (_remaining.Length > 0)
            {
                splitIndex = _remaining.IndexOf(_split);
                if (splitIndex < 0)
                    // Reached the final segment
                    splitIndex = _remaining.Length;

                // Extract current section
                Current = _remaining[..splitIndex];
                if (!Current.IsEmpty)
                    break;

                // Empty segment, skip to next index
                _remaining = _remaining[1..];
            }

            _remaining = _remaining[splitIndex..];
            return !Current.IsEmpty;
        }

        public void Reset()
        {
            Current = ReadOnlySpan<T>.Empty;
            _remaining = _original;
        }
    }

    public static class SpanSplitterExtensions
    {
        public static SpanSplitter<T> Split<T>(this Span<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);

        public static SpanSplitter<T> Split<T>(this ReadOnlySpan<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);
    }
}