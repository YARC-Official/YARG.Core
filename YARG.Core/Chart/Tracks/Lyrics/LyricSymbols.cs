using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Utility;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Definitions and utilities for special symbols in lyric events.
    /// </summary>
    public static class LyricSymbols
    {
        /// <summary>Joins two lyrics together as a single word.</summary>
        /// <remarks>Displayed as-is in vocals, stripped in lyrics.</remarks>
        public const char LYRIC_JOIN_SYMBOL = '-';

        /// <summary>Joins two syllables together and stands in for a hyphen.</summary>
        /// <remarks>Replaced with a hyphen ('-') in vocals and lyrics.</remarks>
        public const char LYRIC_JOIN_HYPHEN_SYMBOL = '=';

        /// <summary>Connects two notes together with a slide from end-to-end.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char PITCH_SLIDE_SYMBOL = '+';


        /// <summary>Marks a note as non-pitched.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_SYMBOL = '#';

        /// <summary>Marks a note as non-pitched, with extra starting leniency.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_LENIENT_SYMBOL = '^';

        /// <summary>Marks a note as non-pitched, but its exact function is unknown.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char NONPITCHED_UNKNOWN_SYMBOL = '*';


        /// <summary>Marks a point at which the vocals track should recalculate its range.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char RANGE_SHIFT_SYMBOL = '%';

        /// <summary>Marks an additional shift point for the static vocals display.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char STATIC_SHIFT_SYMBOL = '/';


        /// <summary>Hides a lyric from being displayed in Harmonies.</summary>
        /// <remarks>Stripped out in both vocals and lyrics.</remarks>
        public const char HARMONY_HIDE_SYMBOL = '$';

        /// <summary>Indicate two lexical syllables are sung as a single phonetic syllable.</summary>
        /// <remarks>
        /// Does not join two lyrics together, it is used within a single syllable specifically.
        /// Replaced with '‿' in vocals, replaced with a space (' ') in lyrics.
        /// </remarks>
        public const char JOINED_SYLLABLE_SYMBOL = '§';

        /// <summary>Stands in for a space (' ').</summary>
        /// <remarks>Only intended for use in lyrics, but will also be replaced in vocals.</remarks>
        public const char SPACE_ESCAPE_SYMBOL = '_';

        /// <summary>All possible lyric symbols.</summary>
        public static readonly HashSet<char> ALL_SYMBOLS = new()
        {
            LYRIC_JOIN_SYMBOL,
            LYRIC_JOIN_HYPHEN_SYMBOL,
            PITCH_SLIDE_SYMBOL,
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
            RANGE_SHIFT_SYMBOL,
            STATIC_SHIFT_SYMBOL,
            HARMONY_HIDE_SYMBOL,
            JOINED_SYLLABLE_SYMBOL,
            SPACE_ESCAPE_SYMBOL,
        };

        /// <summary>Symbols which join two lyrics together.</summary>
        public static readonly HashSet<char> LYRIC_JOIN_SYMBOLS = new()
        {
            LYRIC_JOIN_SYMBOL,
            LYRIC_JOIN_HYPHEN_SYMBOL,
        };

        /// <summary>Symbols which mark a lyric as nonpitched.</summary>
        public static readonly HashSet<char> NONPITCHED_SYMBOLS = new()
        {
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
        };

        /// <summary>Symbols which should be stripped from lyrics in vocals.</summary>
        public static readonly HashSet<char> VOCALS_STRIP_SYMBOLS = new()
        {
            PITCH_SLIDE_SYMBOL,
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
            RANGE_SHIFT_SYMBOL,
            STATIC_SHIFT_SYMBOL,
            HARMONY_HIDE_SYMBOL,

            // Don't display quotation marks in vocals
            '"',
        };

        /// <summary>Symbols which should be stripped from lyrics in the lyrics track.</summary>
        public static readonly HashSet<char> LYRICS_STRIP_SYMBOLS = new()
        {
            LYRIC_JOIN_SYMBOL,
            PITCH_SLIDE_SYMBOL,
            NONPITCHED_SYMBOL,
            NONPITCHED_LENIENT_SYMBOL,
            NONPITCHED_UNKNOWN_SYMBOL,
            RANGE_SHIFT_SYMBOL,
            STATIC_SHIFT_SYMBOL,
            HARMONY_HIDE_SYMBOL,
        };

        /// <summary>Symbols which should be replaced with another in lyrics on vocals.</summary>
        public static readonly Dictionary<char, char> VOCALS_SYMBOL_REPLACEMENTS = new()
        {
            { LYRIC_JOIN_HYPHEN_SYMBOL,     '-' },
            { JOINED_SYLLABLE_SYMBOL,       '‿' },
            { SPACE_ESCAPE_SYMBOL,          ' ' },
        };

        /// <summary>Symbols which should be replaced with another in lyrics on the lyrics track.</summary>
        public static readonly Dictionary<char, char> LYRICS_SYMBOL_REPLACEMENTS = new()
        {
            { LYRIC_JOIN_HYPHEN_SYMBOL,  '-' },
            { JOINED_SYLLABLE_SYMBOL,    ' ' },
            { SPACE_ESCAPE_SYMBOL,       ' ' },
        };

        private static readonly Dictionary<string, string> VOCALS_STRIP_REPLACEMENTS
            = CreateStripReplacements(VOCALS_STRIP_SYMBOLS, VOCALS_SYMBOL_REPLACEMENTS);

        private static readonly Dictionary<string, string> LYRICS_STRIP_REPLACEMENTS
            = CreateStripReplacements(LYRICS_STRIP_SYMBOLS, LYRICS_SYMBOL_REPLACEMENTS);

        private static Dictionary<string, string> CreateStripReplacements(
            HashSet<char> strip, Dictionary<char, char> replace)
        {
            // Add strip characters first to ensure they don't mess with the replacements
            return strip.Select((c) => new KeyValuePair<char, char>(c, '\0'))
                .Concat(replace)
                .ToDictionary(
                    (pair) => pair.Key != '\0' ? pair.Key.ToString() : string.Empty,
                    (pair) => pair.Value != '\0' ? pair.Value.ToString() : string.Empty);
        }

        // Rich text tags allowed by Clone Hero
        // https://strikeline.myjetbrains.com/youtrack/issue/CH-226
        public const RichTextTags LYRICS_ALLOWED_TAGS = RichTextTags.Italics | RichTextTags.Bold |
            RichTextTags.Strikethrough | RichTextTags.Underline | RichTextTags.Superscript | RichTextTags.Subscript |
            RichTextTags.VerticalOffset | RichTextTags.Lowercase | RichTextTags.Uppercase | RichTextTags.SmallCaps |
            RichTextTags.CharSpace | RichTextTags.Color | RichTextTags.Monospace | RichTextTags.LineBreak;

        public static string StripForVocals(string lyric)
        {
            lyric = RichTextUtils.StripRichTextTags(lyric);

            var lyricBuffer = new StringBuilder(lyric);
            foreach (var (symbol, replacement) in VOCALS_STRIP_REPLACEMENTS)
            {
                lyricBuffer.Replace(symbol, replacement);
            }

            return lyricBuffer.ToString();
        }

        public static string StripForLyrics(string lyric)
        {
            lyric = RichTextUtils.StripRichTextTagsExcept(lyric, LYRICS_ALLOWED_TAGS);

            var lyricBuffer = new StringBuilder();
            var segmentBuffer = new StringBuilder();

            // Need to ensure rich text tags are not stripped
            int tagIndex;
            var remaining = lyric.AsSpan();
            while ((tagIndex = remaining.IndexOf('<')) >= 0)
            {
                // Split out segment before the tag
                var segment = remaining[..tagIndex];
                remaining = remaining[tagIndex..];

                // Find end of the tag
                var tag = ReadOnlySpan<char>.Empty;
                int tagCloseIndex = remaining.IndexOf('>');
                if (tagCloseIndex >= 0)
                {
                    // Include closing in tag split
                    tagCloseIndex++;

                    if (tagCloseIndex >= remaining.Length)
                    {
                        tag = remaining;
                        remaining = ReadOnlySpan<char>.Empty;
                    }
                    else
                    {
                        tag = remaining[..tagCloseIndex];
                        remaining = remaining[tagCloseIndex..];
                    }
                }

                // Run through replacements on segment
                if (!segment.IsEmpty)
                {
                    segmentBuffer.Append(segment);
                    foreach (var (symbol, replacement) in LYRICS_STRIP_REPLACEMENTS)
                    {
                        segmentBuffer.Replace(symbol, replacement);
                    }

                    lyricBuffer.Append(segmentBuffer);
                    segmentBuffer.Clear();
                }

                // Insert tag unmodified
                lyricBuffer.Append(tag);
            }

            // Final segment of characters
            if (!remaining.IsEmpty)
            {
                segmentBuffer.Append(remaining);
                foreach (var (symbol, replacement) in LYRICS_STRIP_REPLACEMENTS)
                {
                    segmentBuffer.Replace(symbol, replacement);
                }

                lyricBuffer.Append(segmentBuffer);
            }

            return lyricBuffer.ToString();
        }
    }
}