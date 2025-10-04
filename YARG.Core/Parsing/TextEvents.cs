using System;
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
        /// All text events must be passed through this method before being used elsewhere.
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