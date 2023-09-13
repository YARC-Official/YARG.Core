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
        public readonly T Split => _split;

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

    /// <summary>
    /// A specialized version of <see cref="SpanSplitter{T}"/> that splits <see cref="ReadOnlySpan{char}"/>s,
    /// trimming any leading/trailing whitespace out of the results.
    /// </summary>
    public ref struct TrimSplitter
    {
        private readonly ReadOnlySpan<char> _original;
        private ReadOnlySpan<char> _remaining;
        private readonly char _split;

        public ReadOnlySpan<char> Current { get; private set; }

        public readonly ReadOnlySpan<char> Original => _original;
        public readonly ReadOnlySpan<char> Remaining => _remaining;
        public readonly char Split => _split;

        public TrimSplitter(ReadOnlySpan<char> buffer, char split)
        {
            _original = buffer;
            _remaining = buffer;
            _split = split;
            Current = ReadOnlySpan<char>.Empty;
        }

        public readonly TrimSplitter GetEnumerator() => this;

        public ReadOnlySpan<char> GetNext() => MoveNext() ? Current : ReadOnlySpan<char>.Empty;

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            Current = _remaining.SplitOnceTrimmed(_split, out _remaining);
            return !Current.IsEmpty;
        }

        public void Reset()
        {
            Current = ReadOnlySpan<char>.Empty;
            _remaining = _original;
        }

        public static implicit operator TrimSplitter(SpanSplitter<char> splitter)
        {
            return new(splitter.Original, splitter.Split);
        }

        public static implicit operator SpanSplitter<char>(TrimSplitter splitter)
        {
            return new(splitter.Original, splitter.Split);
        }
    }

    public static class SpanSplitterExtensions
    {
        private interface ISplitGetter<T>
            where T : IEquatable<T>
        {
            ReadOnlySpan<T> GetSegment(ReadOnlySpan<T> buffer, int splitIndex);
            ReadOnlySpan<T> GetRemaining(ReadOnlySpan<T> buffer, int splitIndex);
        }

        private readonly struct SpanSplitGetter<T> : ISplitGetter<T>
            where T : IEquatable<T>
        {
            public readonly ReadOnlySpan<T> GetSegment(ReadOnlySpan<T> buffer, int splitIndex)
                => buffer[..splitIndex];
            public readonly ReadOnlySpan<T> GetRemaining(ReadOnlySpan<T> buffer, int splitIndex)
                => buffer[splitIndex..];
        }

        private readonly struct TrimSplitGetter : ISplitGetter<char>
        {
            public readonly ReadOnlySpan<char> GetSegment(ReadOnlySpan<char> buffer, int splitIndex)
                => buffer[..splitIndex].Trim();
            public readonly ReadOnlySpan<char> GetRemaining(ReadOnlySpan<char> buffer, int splitIndex)
                => buffer[splitIndex..].Trim();
        }

        public static SpanSplitter<char> SplitAsSpan(this string buffer, char split)
            => new(buffer, split);

        public static SpanSplitter<T> Split<T>(this Span<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);

        public static SpanSplitter<T> Split<T>(this ReadOnlySpan<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);

        public static TrimSplitter SplitTrimmed(this string buffer, char split)
            => new(buffer, split);

        public static TrimSplitter SplitTrimmed(this ReadOnlySpan<char> buffer, char split)
            => new(buffer, split);

        public static TrimSplitter SplitTrimmed(this SpanSplitter<char> splitter)
            => splitter;

        public static ReadOnlySpan<char> SplitOnce(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnce(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<T> SplitOnce<T>(this Span<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            => SplitOnce((ReadOnlySpan<T>) buffer, split, out remaining);

        public static ReadOnlySpan<T> SplitOnce<T>(this ReadOnlySpan<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            => buffer.SplitOnce(split, new SpanSplitGetter<T>(), out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmed(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnceTrimmed(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmed(this ReadOnlySpan<char> buffer, char split, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnce(split, new TrimSplitGetter(), out remaining);

        private static ReadOnlySpan<T> SplitOnce<T, TSplit>(this ReadOnlySpan<T> buffer, T split, TSplit splitGetter,
            out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            where TSplit : ISplitGetter<T>
        {
            remaining = buffer;
            var result = ReadOnlySpan<T>.Empty;

            // For ignoring empty splits
            while (result.IsEmpty && !remaining.IsEmpty)
            {
                int splitIndex = remaining.IndexOf(split);
                if (splitIndex < 0)
                    splitIndex = remaining.Length;

                // Split on the value
                result = splitGetter.GetSegment(remaining, splitIndex);

                // Skip the split value and ignore consecutive split values
                while (splitIndex < remaining.Length && remaining[splitIndex].Equals(split))
                    splitIndex++;

                remaining = splitGetter.GetRemaining(remaining, splitIndex);
            }

            return result;
        }
    }
}