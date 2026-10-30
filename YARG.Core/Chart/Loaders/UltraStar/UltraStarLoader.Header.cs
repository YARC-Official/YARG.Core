using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.IO;

namespace YARG.Core.Chart.Loaders.UltraStar
{
    /// <summary>
    /// The '#' tags and voice count of an UltraStar file -- everything a library scan reads.
    /// </summary>
    internal readonly struct UltraStarHeader
    {
        private readonly Dictionary<string, string> _metadata;
        public readonly int VoiceCount;

        public UltraStarHeader(Dictionary<string, string> metadata, int voiceCount)
        {
            _metadata = metadata;
            VoiceCount = voiceCount;
        }

        public string? GetMetadata(string key)
            => _metadata.TryGetValue(key, out var value) ? value : null;
    }

    internal enum UltraStarFileKind
    {
        /// <summary>The first non-blank line doesn't start with '#'.</summary>
        NotAChart,
        Chart,
        /// <summary>Starts with '#' but isn't a tag line with notes, e.g. a Markdown "# Pack notes".</summary>
        NonChartText,
    }

    internal partial class UltraStarLoader
    {
        /// <summary>
        /// Reads only the '#' tags and voice count, with the same decoding and line rules as
        /// the full parser, so it reports exactly what <c>new UltraStarLoader(file)</c> would --
        /// without parsing note lines beyond the first valid one per voice.
        /// </summary>
        internal static UltraStarHeader ScanHeader(FixedArray<byte> file)
        {
            var loader = new UltraStarLoader();
            loader.RunSpanDriver(file, LineProcessMode.Header);
            return new UltraStarHeader(loader._metadata, loader.VoiceCount);
        }

        /// <summary>
        /// Whether a .txt is an UltraStar chart: its first non-blank line is a '#KEY:' tag and
        /// it has at least one populated voice (see <see cref="VoiceCount"/>). A '#' line that
        /// isn't a tag (e.g. a Markdown heading) is never a chart on its own. A broken chart
        /// still scans and reports its error.
        /// </summary>
        /// <remarks>Streams the file rather than loading it in full, and stops at the first note.</remarks>
        internal static UltraStarFileKind ClassifyTextFile(Stream stream)
        {
            var loader = new UltraStarLoader();

            // Same decoding as the loader (UTF-8 default, BOM detection).
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            string? rawLine;
            while ((rawLine = reader.ReadLine()) != null)
            {
                var line = rawLine.Replace('\t', ' ').AsSpan();

                if (!loader._sawFirstLine)
                {
                    var trimmed = line.Trim();
                    if (trimmed.IsEmpty)
                    {
                        continue;
                    }

                    if (trimmed[0] != '#')
                    {
                        return UltraStarFileKind.NotAChart;
                    }

                    // A UTF-16 file without a BOM decodes as "#\0T\0..." -- a real chart, just
                    // one the loader can't read, so it must keep failing loudly rather than be
                    // skipped.
                    if (trimmed.Length > 1 && trimmed[1] == '\0')
                    {
                        return UltraStarFileKind.Chart;
                    }
                }

                if (!loader.ProcessLine(line, LineProcessMode.Classify))
                {
                    break;
                }
            }

            if (!loader._sawFirstLine)
            {
                // Every line was blank (or the file was empty): no first line to classify at all.
                return UltraStarFileKind.NotAChart;
            }

            return loader.StartsWithTag && loader.VoiceCount > 0
                ? UltraStarFileKind.Chart
                : UltraStarFileKind.NonChartText;
        }

        // "#KEY:" -- '#', an ASCII letter, then letters/digits/'_'/'-', optional spaces or
        // tabs, then ':'. A Markdown heading needs whitespace right after '#', so never matches.
        private static bool IsTagLine(ReadOnlySpan<char> trimmedLine)
        {
            if (trimmedLine.Length < 3 || !IsAsciiLetter(trimmedLine[1]))
            {
                return false;
            }

            int i = 2;
            while (i < trimmedLine.Length && (IsAsciiLetter(trimmedLine[i]) || (trimmedLine[i] >= '0' && trimmedLine[i] <= '9')
                || trimmedLine[i] == '_' || trimmedLine[i] == '-'))
            {
                i++;
            }
            while (i < trimmedLine.Length && (trimmedLine[i] == ' ' || trimmedLine[i] == '\t'))
            {
                i++;
            }
            return i < trimmedLine.Length && trimmedLine[i] == ':';
        }

        private static bool IsAsciiLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }
}
