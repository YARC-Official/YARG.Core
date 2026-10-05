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
        /// <summary>Starts with '#' but is plain text, e.g. a Markdown "# Pack notes".</summary>
        NonChartText,
    }

    internal partial class UltraStarLoader
    {
        /// <summary>
        /// Whether a .txt is an UltraStar chart. A '#' first line has always been the test,
        /// but on its own it also matches Markdown notes packs ship with. So a '#' line that
        /// isn't a tag only counts when the file also has a note line the parser would accept
        /// -- a genuinely broken chart still scans (and reports its error), while a notes file
        /// no longer reads as a bad song.
        /// </summary>
        /// <remarks>
        /// Reads only as far as it needs -- normally just the first line -- and leaves the
        /// stream open, wherever reading stopped.
        /// </remarks>
        internal static UltraStarFileKind ClassifyTextFile(Stream stream)
        {
            // Same decoding as the loader (UTF-8 default, BOM detection).
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var firstLine = line.AsSpan().Trim();
                if (firstLine.IsEmpty)
                {
                    continue;
                }

                if (firstLine[0] != '#')
                {
                    return UltraStarFileKind.NotAChart;
                }

                // A UTF-16 file without a BOM decodes as "#\0T\0..." -- a real chart, just one
                // the loader can't read, so it must keep failing loudly rather than be skipped.
                if (IsTagLine(firstLine) || (firstLine.Length > 1 && firstLine[1] == '\0'))
                {
                    return UltraStarFileKind.Chart;
                }

                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.AsSpan().Trim();
                    if (ClassifyLine(trimmed, out _) == LineKind.Note && !UltraStarNote.IsRestType(trimmed[0])
                        && TryParseNoteFields(SplitNoteFields(line.TrimStart()), out _, out _, out _))
                    {
                        return UltraStarFileKind.Chart;
                    }
                }
                return UltraStarFileKind.NonChartText;
            }
            return UltraStarFileKind.NotAChart;
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

        /// <summary>
        /// Reads only the '#' tags and voice count, with the same decoding and line rules as
        /// the full parser, so it reports exactly what <c>new UltraStarLoader(file)</c> would --
        /// without parsing note lines beyond the first valid one per voice.
        /// </summary>
        internal static UltraStarHeader ScanHeader(FixedArray<byte> file)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int currentPart = 0;
            // One bit per voice part (P1..P3) that the full parser would create.
            int partMask = 0;

            var text = DecodeLikeLoader(file).AsSpan();
            while (TryReadLine(ref text, out var line))
            {
                var trimmed = line.Trim();
                var kind = ClassifyLine(trimmed, out int voiceNumber);
                if (kind == LineKind.End)
                {
                    break;
                }

                switch (kind)
                {
                    case LineKind.Metadata:
                        if (TryParseMetadataLine(trimmed, out string key, out string value))
                        {
                            metadata[key] = value;
                        }
                        break;
                    case LineKind.VoiceMarker:
                        if (IsSupportedVoice(voiceNumber))
                        {
                            currentPart = voiceNumber - 1;
                            partMask |= 1 << currentPart;
                        }
                        break;
                    case LineKind.Note:
                        // A note or rest line only creates its voice's part if it parses -- the
                        // same check ParseNoteLine makes before adding the note.
                        if ((partMask & (1 << currentPart)) == 0)
                        {
                            var fields = SplitNoteFields(line.TrimStart().ToString());
                            bool parses = fields.Length >= 1 && (UltraStarNote.IsRestType(fields[0][0])
                                ? TryParseRestBeat(fields, out _)
                                : TryParseNoteFields(fields, out _, out _, out _));
                            if (parses)
                            {
                                partMask |= 1 << currentPart;
                            }
                        }
                        break;
                }
            }

            int voiceCount = (partMask & 1) + ((partMask >> 1) & 1) + ((partMask >> 2) & 1);
            return new UltraStarHeader(metadata, voiceCount);
        }

        // Same reader setup as ParseUltraStarFile (UTF-8 default, BOM detection), so both
        // see identical characters.
        private static string DecodeLikeLoader(FixedArray<byte> file)
        {
            using var reader = new StreamReader(file.ToReferenceStream(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Splits on "\r", "\n" or "\r\n", like StreamReader.ReadLine.
        private static bool TryReadLine(ref ReadOnlySpan<char> text, out ReadOnlySpan<char> line)
        {
            if (text.IsEmpty)
            {
                line = default;
                return false;
            }

            int end = text.IndexOfAny('\r', '\n');
            if (end < 0)
            {
                line = text;
                text = ReadOnlySpan<char>.Empty;
                return true;
            }

            line = text[..end];
            int next = end + 1;
            if (text[end] == '\r' && next < text.Length && text[next] == '\n')
            {
                next++;
            }
            text = text[next..];
            return true;
        }
    }
}
