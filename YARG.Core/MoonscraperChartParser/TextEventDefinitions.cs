using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song
{
    /// <summary>
    /// Constants for possible text events.
    /// </summary>
    internal static class TextEventDefinitions
    {
        #region Global lyric events
        public const string
        LYRIC_PREFIX = "lyric",
        LYRIC_PREFIX_WITH_SPACE = LYRIC_PREFIX + " ",
        LYRIC_PHRASE_START = "phrase_start",
        LYRIC_PHRASE_END = "phrase_end";
        #endregion

        #region Solos
        public const string
        SOLO_START = "solo",
        SOLO_END = "soloend";
        #endregion
    }
}