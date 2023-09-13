using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Parsing
{
    using TrimSplitter = SpanSplitter<char, TrimSplitProcessor>;

    /// <summary>
    /// A .chart file, containing any number of sections.
    /// </summary>
    public ref struct DotChartFile
    {
        /// <summary>
        /// A <see cref="DotChartFile"/> which contains no data.
        /// </summary>
        public static DotChartFile Empty => new();

        private readonly ReadOnlySpan<char> _original;
        private ReadOnlySpan<char> _remaining;

        /// <summary>
        /// Whether or not the file contains any data.<br/>
        /// No actual validation on the data is performed, this simply checks if any non-whitespace data is present.
        /// </summary>
        public readonly bool IsEmpty => _original.IsWhiteSpace();

        /// <summary>
        /// The currently-enumerated section from the file.
        /// </summary>
        public DotChartSection Current { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public DotChartFile(ReadOnlySpan<char> chartText)
        {
            _remaining = _original = chartText;
            Current = default;
        }

        /// <summary>
        /// For enumerator support; returns this same instance.
        /// </summary>
        public readonly DotChartFile GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            var section = DotChartSection.Empty;
            while (section.IsEmpty)
            {
                section = FindNextSection(ref _remaining);
                if (section.Name.IsEmpty)
                    // No more sections found
                    return false;
            }

            Current = section;
            return true;
        }

        public static DotChartSection FindNextSection(ref ReadOnlySpan<char> remaining)
        {
            // Find section name
            int nameIndex = remaining.IndexOf('[');
            int nameEndIndex = remaining.IndexOf(']');
            if (nameIndex < 0 || nameEndIndex < 0 || nameEndIndex < nameIndex)
                // No more sections found
                return DotChartSection.Empty;

            nameIndex++; // Exclude starting bracket
            var sectionName = remaining[nameIndex..nameEndIndex];
            remaining = remaining[nameEndIndex..];

            // Find section body
            int sectionIndex = remaining.IndexOf('{');
            int sectionEndIndex = remaining.IndexOf('}');
            if (sectionIndex < 0 || sectionEndIndex < 0 || sectionEndIndex < sectionIndex)
                // No section body found in the rest of the file
                return DotChartSection.Empty;

            sectionIndex++; // Exclude starting bracket
            var sectionText = remaining[sectionIndex..sectionEndIndex].Trim();
            remaining = remaining[sectionEndIndex..];

            return new(sectionName, sectionText);
        }

        public void Reset()
        {
            _remaining = _original;
            Current = default;
        }
    }

    /// <summary>
    /// A .chart section, consisting of key-value pairs.
    /// </summary>
    public ref struct DotChartSection
    {
        public static DotChartSection Empty => new();

        public readonly ReadOnlySpan<char> Name;
        private readonly ReadOnlySpan<char> _original;
        private readonly TrimSplitter _lines;

        public readonly bool IsEmpty => _original.IsWhiteSpace();
        public readonly int EstimatedCount => _original.Length / _original.SplitOnce('\n', out _).Length;

        public DotChartLine Current { get; private set; }

        public DotChartSection(ReadOnlySpan<char> name, ReadOnlySpan<char> text)
        {
            Name = name;
            _original = text;
            _lines = text.SplitTrimmed('\n');
            Current = default;
        }

        public readonly TrimSplitter GetLines() => _lines;

        public readonly DotChartSection GetEnumerator() => this;

        public bool MoveNext()
        {
            if (!_lines.MoveNext() || _lines.Current.IsEmpty)
                return false;

            Current = new(_lines.Current);
            return true;
        }

        public void Reset()
        {
            _lines.Reset();
            Current = default;
        }
    }

    /// <summary>
    /// A single line of a .chart file, comprised of a key-value pair.
    /// </summary>
    public readonly ref struct DotChartLine
    {
        public static DotChartLine Empty => new();

        public readonly ReadOnlySpan<char> Full;

        public readonly ReadOnlySpan<char> Key;
        public readonly ReadOnlySpan<char> Value;

        public DotChartLine(ReadOnlySpan<char> line)
        {
            Full = line;
            Key = line.SplitOnceTrimmed('=', out Value);
        }
    }

    /// <summary>
    /// An event in a .chart file that occurs at a specific tick.
    /// </summary>
    public readonly ref struct DotChartTickEvent
    {
        public static DotChartTickEvent Empty => new();

        public readonly uint Tick;
        public readonly ReadOnlySpan<char> Type;
        public readonly ReadOnlySpan<char> Value;

        public DotChartTickEvent(uint tick, ReadOnlySpan<char> type, ReadOnlySpan<char> value)
        {
            Tick = tick;
            Type = type;
            Value = value;
        }

        public DotChartTickEvent(DotChartLine line)
        {
            Tick = uint.Parse(line.Key);
            Type = line.Value.SplitOnceTrimmed(' ', out Value);
        }

        public DotChartParameters GetParameters(int minCount = 0, int maxCount = -1)
            => new(Value, minCount, maxCount);

        public static bool TryParse(DotChartLine line, out DotChartTickEvent tickEvent)
        {
            tickEvent = default;
            if (!uint.TryParse(line.Key, out uint tick))
                return false;

            var type = line.Value.SplitOnceTrimmed(' ', out var parameters);
            if (type.IsEmpty || parameters.IsEmpty)
                return false;

            tickEvent = new(tick, type, parameters);
            return true;
        }
    }

    /// <summary>
    /// The parameters contained within a single .chart line's value.
    /// </summary>
    public ref struct DotChartParameters
    {
        public static DotChartParameters Empty => new();

        public readonly int MinimumCount;
        public readonly int MaximumCount;

        private readonly ReadOnlySpan<char> _original;
        private ReadOnlySpan<char> _remaining;
        private int _parameterCount;

        public ReadOnlySpan<char> Current { get; private set; }

        public DotChartParameters(ReadOnlySpan<char> value, int minCount = 0, int maxCount = -1)
        {
            if (maxCount < 0)
                maxCount = minCount;

            MinimumCount = minCount;
            MaximumCount = maxCount;

            _remaining = _original = value;

            _parameterCount = 0;
            Current = ReadOnlySpan<char>.Empty;
        }

        public readonly DotChartParameters GetEnumerator() => this;

        public ReadOnlySpan<char> GetNext() => MoveNext() ? Current : ReadOnlySpan<char>.Empty;

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            // Quoted parameters
            var value = _remaining[0] == '"'
                ? HandleQuotedParameter()
                : HandleStandardParameter();
            if (value.IsEmpty)
                return false;

            Current = value;
            _parameterCount++;
            return true;
        }

        private ReadOnlySpan<char> HandleStandardParameter()
        {
            var value = _remaining.SplitOnceTrimmed(' ', out _remaining);
            if (value.IsEmpty)
            {
                if (_parameterCount < MinimumCount)
                    throw new InvalidDataException($"Not enough parameters in '{_original.ToString()}'! Expected {MinimumCount}, found {_parameterCount}");
                return ReadOnlySpan<char>.Empty;
            }

            return value;
        }

        private ReadOnlySpan<char> HandleQuotedParameter()
        {
            // Skip starting quote
            var remaining = _remaining[1..];

            // Handle the entire remaining text as the parameter if only one more is expected
            if (_parameterCount + 1 == MaximumCount)
            {
                var value = remaining.Trim('"');
                _remaining = ReadOnlySpan<char>.Empty;
                return value;
            }

            // Find the end of the string
            int endIndex = remaining.IndexOf('"');
            if (endIndex < 0)
                throw new InvalidDataException($"Unterminated string in '{_original.ToString()}'!");

            // TODO: Some heuristics to account for quotation marks inside of strings?
            // Becuse no one's ever standardized escape characters for .chart...
            return remaining[..endIndex];
        }

        public void Reset()
        {
            _remaining = _original;
            Current = ReadOnlySpan<char>.Empty;
        }
    }
}