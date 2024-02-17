using System;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal static partial class DotChartParser
    {
        private const string SONG_SECTION = "Song";
        private const string SYNC_TRACK_SECTION = "SyncTrack";
        private const string EVENTS_SECTION = "Events";

        public static SongChart ParseChart(in ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            var chart = new SongChart();
            int textIndex = 0;

            static void ExpectSection(ReadOnlySpan<char> chartText, ref int textIndex,
                string name, out AsciiTrimSplitter sectionBody)
            {
                if (!GetNextSection(chartText, ref textIndex, out var sectionName, out sectionBody) ||
                    !sectionName.Equals(name, StringComparison.Ordinal))
                {
                    throw new Exception($"Invalid section ordering! Expected [{name}], found [{sectionName.ToString()}]");
                }
            }

            // Check for the [Song] section first explicitly, need the Resolution property up-front
            ExpectSection(chartText, ref textIndex, SONG_SECTION, out var sectionBody);
            var metadata = ParseMetadata(sectionBody);

            // Check for [SyncTrack] next, we need it for time conversions
            ExpectSection(chartText, ref textIndex, SYNC_TRACK_SECTION, out sectionBody);
            new DotChartSyncTrackHandler(chart, metadata.Resolution).ParseSection(sectionBody);

            // Lastly, check for [Events]
            // Not strictly necessary, but may as well handle it specifically
            ExpectSection(chartText, ref textIndex, EVENTS_SECTION, out sectionBody);

            // Parse instrument tracks
            while (GetNextSection(chartText, ref textIndex, out var sectionName, out sectionBody))
            {
            }

            return chart;
        }

        private static bool GetNextSection(ReadOnlySpan<char> chartText, ref int index,
            out ReadOnlySpan<char> sectionName, out AsciiTrimSplitter sectionBody)
        {
            static int GetLineCount(ReadOnlySpan<char> chartText, int startIndex, int relativeIndex)
            {
                return chartText[..(startIndex + relativeIndex)].Count('\n');
            }

            sectionName = default;
            sectionBody = default;
            if (index >= chartText.Length)
                // No more sections present
                return false;

            var search = chartText[index..];

            // Find section name
            int nameStartIndex = search.IndexOf('[');
            int nameEndIndex = search.IndexOf(']');
            if (nameStartIndex < 0)
                // No more sections present
                return false;

            if (nameEndIndex < 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Missing end bracket for section name on line {startLine}!");
            }

            if (nameEndIndex < nameStartIndex)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                int endLine = GetLineCount(chartText, index, nameEndIndex);
                if (startLine == endLine)
                    throw new Exception($"Misordered section name brackets on line {startLine}!");
                else
                    throw new Exception($"Misordered section name brackets! Start bracket on line {startLine}, end on line {endLine}");
            }

            sectionName = search[++nameStartIndex..nameEndIndex];
            search = search[nameEndIndex..];
            index += nameEndIndex;

            if (sectionName.IndexOfAny('\r', '\n') >= 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Section name on {startLine} spans across multiple lines!");
            }

            // Find section body
            int sectionStartIndex = search.IndexOf('{');
            int sectionEndIndex = search.IndexOf('}');
            if (sectionStartIndex < 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Missing section body for section [{sectionName.ToString()}]! (starting on line {startLine})");
            }

            if (sectionEndIndex < 0)
            {
                int startLine = GetLineCount(chartText, index, nameStartIndex);
                throw new Exception($"Missing body end bracket for section [{sectionName.ToString()}]! (starting on line {startLine})");
            }

            if (sectionEndIndex < sectionStartIndex)
            {
                int startLine = GetLineCount(chartText, index, sectionStartIndex);
                int endLine = GetLineCount(chartText, index, sectionEndIndex);
                throw new Exception($"Misordered section body brackets! Start bracket on line {startLine}, end on line {endLine}");
            }

            sectionBody = search[++sectionStartIndex..sectionEndIndex].SplitTrimmedAscii('\n');
            index += ++sectionEndIndex;
            return true;
        }
    }
}