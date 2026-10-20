using System;
using System.Globalization;
using YARG.Core.Extensions;
using YARG.Core.IO;
using MemoryExtensions = System.MemoryExtensions;

namespace YARG.Core.Parsing
{
    /// <summary>
    /// Available stem configurations for a drums mix event.
    /// </summary>
    public enum DrumsMixConfiguration
    {
        StereoKit = 0,
        NoStem = StereoKit,
        MonoKickSnare_StereoKit = 1,
        MonoKick_StereoSnareKit = 2,
        StereoKickSnareKit = 3,
        MonoKick_StereoKit = 4,
        StereoKickSnareTomCymbal = 5,
    }

    /// <summary>
    /// Additional settings for a drums mix event.
    /// </summary>
    public enum DrumsMixSetting
    {
        None = 0,

        /// <summary>
        /// Swap the red and yellow lanes on Pro Drums, along with their assigned stems.
        /// </summary>
        DiscoFlip = 1,

        /// <summary>
        /// Swap the stems assigned to the red/yellow lanes without swapping the notes.
        /// </summary>
        DiscoNoFlip = 2,

        /// <summary>
        /// Force-unmute the tom and cymbal stems on Easy.
        /// </summary>
        Easy = 3,

        /// <summary>
        /// Force-unmute the kick stem on Easy.
        /// </summary>
        EasyNoKick = 4,
    }

    public enum EliteDrumsSnareStemSetting
    {
        Red = 0,
        Yellow = 1,
        Blue = 2,
        Green = 3,
    }

    /// <summary>
    /// Possible crowd states
    /// </summary>
    public enum CrowdState
    {
        None = 0,
        Realtime,
        Intense,
        Normal,
        Mellow,
    }

    public enum ClapState {
        None = 0,
        Clap,
        NoClap
    }

    /// <summary>
    /// Constants and utilities for handling text events.
    /// </summary>
    public static partial class TextEvents
    {
        public const string BIG_ROCK_ENDING_START = "coda";
        public const string END_MARKER            = "end";
        public const string MUSIC_START           = "music_start";
        public const string MUSIC_END             = "music_end";

        /// <summary>
        /// Normalizes text events into a consistent format. This includes stripping any
        /// leading/trailing whitespace, and isolating any text inside square brackets.
        /// </summary>
        /// <remarks>
        /// All text events must be passed through this method, or <see cref="NormalizeGlobalEvent"/> for song-wide
        /// events, before being used elsewhere.
        /// All other methods that operate on text events expect them to be normalized.
        /// </remarks>
        // Equivalent to reading the capture of this regex: \[(.*?)\]
        public static ReadOnlySpan<char> NormalizeTextEvent(ReadOnlySpan<char> text, out bool hadBrackets)
        {
            int startIndex = text.IndexOf('[');
            int endIndex = text.IndexOf(']');
            if (startIndex >= 0 && endIndex >= 0 && startIndex <= endIndex)
            {
                hadBrackets = true;
                return text[++startIndex..endIndex].TrimAscii();
            }
            else
            {
                hadBrackets = false;
                return text.TrimAscii();
            }
        }

        /// <inheritdoc cref="NormalizeTextEvent(ReadOnlySpan{char}, out bool)"/>
        public static ReadOnlySpan<char> NormalizeTextEvent(ReadOnlySpan<char> text)
            => NormalizeTextEvent(text, out _);

        /// <summary>
        /// Normalizes a song-wide text event (.chart [Events], .mid EVENTS). The trimmed text is read as written
        /// when it's an event. Otherwise, when the text has a '[' and a ']' that closes it (counting nested pairs),
        /// the text between the first '[' and that ']' is read as it is, and everything outside them is dropped.
        /// When the text starts with a '[' that no ']' closes, the text after that '[' is read as it is.
        /// </summary>
        /// <remarks>
        /// For example, <c>section Verse [2]</c> is a section named <c>Verse [2]</c>,
        /// <c>[prc_intro] - Intro</c> is a section named <c>intro</c>, <c>[coda</c> is the coda,
        /// and <c>x [coda</c> is no event.
        /// </remarks>
        /// <param name="text">The event's text.</param>
        /// <param name="holdsLyrics">
        /// Whether the events hold lyrics, as a .chart file's [Events] does and a .mid file's EVENTS doesn't.
        /// Only then are lyrics, <c>phrase_start</c> and <c>phrase_end</c> events here.
        /// </param>
        public static ReadOnlySpan<char> NormalizeGlobalEvent(ReadOnlySpan<char> text, bool holdsLyrics)
        {
            text = text.Trim();
            if (IsGlobalEvent(text, holdsLyrics))
                return text;

            int open = text.IndexOf('[');
            if (open < 0)
                return text;

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    depth++;
                }
                else if (text[i] == ']' && --depth == 0)
                {
                    return text[(open + 1)..i];
                }
            }

            // A '[' at the start that no ']' closes
            return open == 0 ? text[1..] : text;
        }

        // The song-wide events of the chart specification, which decide how brackets are read.
        // YARG's own events, such as crowd events, aren't among them, and read as they are after normalizing.
        private static bool IsGlobalEvent(ReadOnlySpan<char> text, bool holdsLyrics)
        {
            return TryParseSectionEvent(text, out _)
                || text.Equals(BIG_ROCK_ENDING_START, StringComparison.Ordinal)
                || text.Equals(END_MARKER, StringComparison.Ordinal)
                || (holdsLyrics && (text.StartsWith(LYRIC_PREFIX_WITH_SPACE)
                    || text.StartsWith(LYRIC_PREFIX + "\t")
                    || text.Equals(LYRIC_PHRASE_START, StringComparison.Ordinal)
                    || text.Equals(LYRIC_PHRASE_END, StringComparison.Ordinal)));
        }

        // For events that have either space or underscore separators
        private static ReadOnlySpan<char> SkipSpaceOrUnderscore(this ReadOnlySpan<char> text)
        {
            return text.TrimStart('_').TrimStart();
        }

        /// <summary>
        /// Parses a section name from a text event.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        // Equivalent to reading the capture of this regex: (?:section|prc)[ _](.*)
        public static bool TryParseSectionEvent(ReadOnlySpan<char> text, out ReadOnlySpan<char> name)
        {
            name = ReadOnlySpan<char>.Empty;

            const string SECTION_PREFIX = "section";
            const string PRC_PREFIX = "prc";

            // Remove event prefix
            if (text.StartsWith(SECTION_PREFIX))
                text = text[SECTION_PREFIX.Length..];
            else if (text.StartsWith(PRC_PREFIX))
                text = text[PRC_PREFIX.Length..];
            else
                return false;

            // Isolate section name
            name = text.TrimStart('_').Trim();
            return !name.IsEmpty;
        }

        /// <summary>
        /// Parses a 5-fret range shift event: <c>ld_range_shift &lt;difficulty&gt; &lt;position&gt; [size]</c>,
        /// with the parts separated by single spaces and the numbers in digits.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        public static bool TryParseRangeShiftEvent(string text, out Difficulty difficulty, out int position, out int size)
        {
            difficulty = Difficulty.Expert;
            position = 0;
            size = 0;

            const string RANGE_SHIFT_PREFIX = "ld_range_shift";
            if (!text.StartsWith(RANGE_SHIFT_PREFIX + " ", StringComparison.Ordinal))
                return false;

            var parts = text.Split(' ');
            // 3 parts use the difficulty's default size, 4 give the size
            if (parts.Length is not (3 or 4))
                return false;

            if (!TryParseDigits(parts[1], out int difficultyNumber) || difficultyNumber > 3
                || !TryParseDigits(parts[2], out position) || position is < 1 or > 5)
                return false;

            // Difficulties are numbered from 0, Easy, to 3, Expert
            difficulty = (Difficulty) (difficultyNumber + 1);
            if (parts.Length == 4)
            {
                if (!TryParseDigits(parts[3], out size) || size is < 1 or > 5)
                    return false;
            }
            else
            {
                size = difficulty switch
                {
                    Difficulty.Easy   => 3,
                    Difficulty.Medium => 4,
                    _                 => 5,
                };
            }

            // The range can't go past the fifth fret
            return position + size <= 6;

            static bool TryParseDigits(string number, out int value)
                => int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Parses mix info from a drums mix event.
        /// </summary>
        /// <returns>
        /// True if the event was parsed successfully, false otherwise.
        /// </returns>
        public static bool TryParseDrumsMixEvent(ReadOnlySpan<char> text, out Difficulty difficulty,
            out DrumsMixConfiguration config, out DrumsMixSetting setting)
        {
            difficulty = Difficulty.Expert;
            config = DrumsMixConfiguration.NoStem;
            setting = DrumsMixSetting.None;

            // Remove event prefix
            const string MIX_PREFIX = "mix";
            if (!text.StartsWith(MIX_PREFIX))
                return false;
            text = text[MIX_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse difficulty number
            if (!text[0].TryAsciiToNumber(out uint diffNumber))
                return false;
            text = text[1..].SkipSpaceOrUnderscore();

            switch (diffNumber)
            {
                case 3: difficulty = Difficulty.Expert; break;
                case 2: difficulty = Difficulty.Hard; break;
                case 1: difficulty = Difficulty.Medium; break;
                case 0: difficulty = Difficulty.Easy; break;
                default: return false;
            }

            // Skip 'drums' text
            const string DRUMS_PREFIX = "drums";
            if (!text.StartsWith(DRUMS_PREFIX))
                return false;
            text = text[DRUMS_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse configuration number
            if (!text[0].TryAsciiToNumber(out uint configNumber) || configNumber > 5)
                return false;
            text = text[1..].SkipSpaceOrUnderscore();

            config = (DrumsMixConfiguration) configNumber;

            // Parse settings
            var settingText = text;
            if (settingText.Equals("d", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoFlip;
            else if (settingText.Equals("dnoflip", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoNoFlip;
            else if (settingText.Equals("easy", StringComparison.Ordinal))
                setting = DrumsMixSetting.Easy;
            else if (settingText.Equals("easynokick", StringComparison.Ordinal))
                setting = DrumsMixSetting.EasyNoKick;
            else
                setting = DrumsMixSetting.None;

            return true;
        }

        // setting is 0 for red, 1 for yellow, 2 for blue, 3 for green
        public static bool TryParseEliteDrumsSnareStemEvent(ReadOnlySpan<char> text, out EliteDrumsSnareStemSetting setting, out Difficulty difficulty)
        {
            difficulty = Difficulty.Expert;
            setting = 0;

            const string MIX_PREFIX = "snare_stem";
            if (!text.StartsWith(MIX_PREFIX))
                return false;
            text = text[MIX_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse difficulty number
            if (!text[0].TryAsciiToNumber(out uint pad))
                return false;
            text = text[1..].SkipSpaceOrUnderscore();

            switch (pad)
            {
                case 0 or 1 or 2 or 3: setting = (EliteDrumsSnareStemSetting)pad; break;
                default: return false;
            }

            // Skip space
            text = text.SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse difficulty number
            if (text[0].TryAsciiToNumber(out uint diffNumber))
            {
                switch (diffNumber)
                {
                    case 3: difficulty = Difficulty.Expert; break;
                    case 2: difficulty = Difficulty.Hard; break;
                    case 1: difficulty = Difficulty.Medium; break;
                    case 0: difficulty = Difficulty.Easy; break;
                    default: break;
                }
            }

            return true;
        }

        public static bool TryParseCrowdEvent(ReadOnlySpan<char> text, out CrowdState state)
        {
            state = CrowdState.Normal;
            if (!text.StartsWith(CROWD_PREFIX))
            {
                return false;
            }

            // If we had C# 11, we could use a switch expression without having to take the allocation
            // from ToString, but alas we do not.
            var crowdText = text.ToString();

            CrowdState? crowdState = crowdText switch
            {
                CROWD_REALTIME => CrowdState.Realtime,
                CROWD_INTENSE  => CrowdState.Intense,
                CROWD_NORMAL   => CrowdState.Normal,
                CROWD_MELLOW   => CrowdState.Mellow,
                _                => null
            };

            if (crowdState is null)
            {
                // Not a valid crowd state
                return false;
            }

            state = crowdState.Value;
            return true;
        }

        public static bool TryParseClapEvent(ReadOnlySpan<char> text, out ClapState state)
        {
            state = ClapState.Clap;

            if (!text.StartsWith(CROWD_PREFIX))
            {
                return false;
            }

            if (text.Equals(CROWD_CLAP, StringComparison.Ordinal))
            {
                return true;
            }

            if (text.Equals(CROWD_NOCLAP, StringComparison.Ordinal))
            {
                state = ClapState.NoClap;
                return true;
            }

            // Right prefix, not valid text
            return false;
        }
    }
}