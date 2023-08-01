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

            Current = _remaining.SplitOnce(_split, out _remaining);
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

        public static ReadOnlySpan<T> SplitOnce<T>(this Span<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            => SplitOnce((ReadOnlySpan<T>)buffer, split, out remaining);

        public static ReadOnlySpan<T> SplitOnce<T>(this ReadOnlySpan<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
        {
            remaining = buffer;

            // `while` to ignore splits that consist of only the split value
            while (!remaining.IsEmpty)
            {
                int splitIndex = remaining.IndexOf(split);
                if (splitIndex < 0)
                {
                    // No result, use the rest of the buffer
                    var finalResult = remaining;
                    remaining = ReadOnlySpan<T>.Empty;
                    return finalResult;
                }

                // Split on the value
                var result = remaining[..splitIndex];

                // Skip the split value
                if (splitIndex < remaining.Length)
                    splitIndex++;
                remaining = remaining[splitIndex..];

                // Ignore empty splits
                if (!result.IsEmpty)
                    return result;
            }

            return ReadOnlySpan<T>.Empty;
        }

        public static ReadOnlySpan<char> SplitOnceTrim(this ReadOnlySpan<char> buffer, char split, out ReadOnlySpan<char> remaining)
        {
            var result = buffer.SplitOnce(split, out remaining).Trim();
            remaining = remaining.TrimStart(); // Trim only the start, leave the rest untouched
            return result;
        }
    }
}