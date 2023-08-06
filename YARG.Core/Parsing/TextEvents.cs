using System;

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

    /// <summary>
    /// Text events that can be found in both .chart and .mid.
    /// </summary>
    public static class TextEvents
    {
        public const string SECTION_PREFIX_1 = "section";
        public const string SECTION_PREFIX_2 = "prc";

        public const string BIG_ROCK_ENDING_START = "coda";
        public const string END_MARKER = "end";

        public const string DRUMS_MIX_PREFIX = "mix";

        /// <summary>
        /// Normalizes text events into a consistent format. This includes stripping any
        /// leading/trailing whitespace, and isolating any text inside square brackets.
        /// </summary>
        /// <remarks>
        /// All text events must be passed through this method before being used elsewhere.
        /// All other methods that operate on text events expect them to be normalized.
        /// </remarks>
        // Equivalent to reading the capture of this regex: \[(.*?)\]
        public static void NormalizeTextEvent(ref ReadOnlySpan<char> text, out bool strippedBrackets)
        {
            // Trim leading/trailing whitespace
            text = text.Trim();

            // Isolate text inside brackets
            // Find the starting bracket
            strippedBrackets = false;
            int startIndex = text.IndexOf('[');
            if (startIndex < 0)
                return;
            startIndex++;

            // Find the ending bracket
            int lastIndex = text[startIndex..].IndexOf(']');
            if (lastIndex < 0)
                return;

            text = text[startIndex..lastIndex].Trim();
            strippedBrackets = true;
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

            // Remove event prefix
            if (text.StartsWith(SECTION_PREFIX_1))
                text = text[SECTION_PREFIX_1.Length..];
            else if (text.StartsWith(SECTION_PREFIX_2))
                text = text[SECTION_PREFIX_2.Length..];
            else
                return false;

            // Isolate section name
            name = text.TrimStart('_').Trim();
            return !name.IsEmpty;
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
            if (!text.StartsWith(DRUMS_MIX_PREFIX))
                return false;
            text = text[DRUMS_MIX_PREFIX.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse difficulty number
            var diffText = text[0..1];
            text = text[1..].SkipSpaceOrUnderscore();
            if (!uint.TryParse(diffText, out uint diffNumber))
                return false;

            switch (diffNumber)
            {
                case 3: difficulty = Difficulty.Expert; break;
                case 2: difficulty = Difficulty.Hard; break;
                case 1: difficulty = Difficulty.Medium; break;
                case 0: difficulty = Difficulty.Easy; break;
                default: return false;
            }

            // Skip 'drums' text
            const string DrumsText = "drums";
            if (!text.StartsWith(DrumsText))
                return false;
            text = text[DrumsText.Length..].SkipSpaceOrUnderscore();
            if (text.IsEmpty)
                return false;

            // Parse configuration number
            var configText = text[0..1];
            text = text[1..].SkipSpaceOrUnderscore();
            if (!uint.TryParse(configText, out uint configNumber) || configNumber > 5)
                return false;

            config = (DrumsMixConfiguration)configNumber;

            // Parse settings
            var settingsText = text;
            if (settingsText.Equals("d", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoFlip;
            else if (settingsText.Equals("dnoflip", StringComparison.Ordinal))
                setting = DrumsMixSetting.DiscoNoFlip;
            else if (settingsText.Equals("easy", StringComparison.Ordinal))
                setting = DrumsMixSetting.Easy;
            else if (settingsText.Equals("easynokick", StringComparison.Ordinal))
                setting = DrumsMixSetting.EasyNoKick;
            else
                setting = DrumsMixSetting.None;

            return true;
        }
    }
}