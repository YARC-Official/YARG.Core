using System;
using YARG.Core.Extensions;

namespace YARG.Core.Chart.Parsing
{
    /// <summary>
    /// Text events specific to .chart.
    /// </summary>
    internal static class DotChartTextEvents
    {
        public const string SOLO_START = "solo";
        public const string SOLO_END = "soloend";

        public const string LYRICS_PHRASE_START = "phrase_start";
        public const string LYRICS_PHRASE_END = "phrase_end";
        public const string LYRIC_PREFIX = "lyric";

        /// <summary>
        /// Parses out a lyric from a text event.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        // Equivalent to reading the capture of this regex: lyric\w*(.*)
        public static bool TryParseLyricEvent(ReadOnlySpan<char> text, out ReadOnlySpan<char> lyric)
        {
            lyric = ReadOnlySpan<char>.Empty;

            if (!text.StartsWith(LYRIC_PREFIX))
                return false;

            lyric = text[LYRIC_PREFIX.Length..].TrimStartAscii();
            return true;
        }
    }
}