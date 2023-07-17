namespace MoonscraperChartEditor.Song
{
    /// <summary>
    /// Constants for possible text events.
    /// </summary>
    internal static class TextEventDefinitions
    {
        // Global lyric events
        public const string
        LYRIC_PREFIX = "lyric ",
        LYRIC_PHRASE_START = "phrase_start",
        LYRIC_PHRASE_END = "phrase_end";

        // Solos
        public const string
        SOLO_START = "solo",
        SOLO_END = "soloend";
    }
}