using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    internal interface ITextPhraseConverter
    {
        string StartEvent { get; }
        string EndEvent { get; }

        void AddPhrase(uint startTick, uint endTick, bool nextPhraseStarted);
        void AddPhraseEvent(string text, uint tick);
    }

    /// <summary>
    /// Constants for possible text events.
    /// </summary>
    internal static class TextEvents
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

        #region Text event to phrase conversion
        public static void ConvertToPhrases(List<MoonText> events, ITextPhraseConverter converter)
        {
            string startEvent = converter.StartEvent;
            string endEvent = converter.EndEvent;

            uint currentTick = 0; 
            uint? startTick = null;
            bool start = false;
            bool end = false;

            for (int i = 0; i < events.Count; i++)
            {
                var textEv = events[i];
                uint tick = textEv.tick;
                string text = textEv.text;

                // Commit found events on start of next tick
                if (tick != currentTick)
                {
                    ProcessPhraseEvents(converter, currentTick, ref startTick, start, end);
                    currentTick = tick;
                    start = end = false;
                }

                // Determine what events are present on the current tick
                if (text == startEvent)
                {
                    events.RemoveAt(i);
                    i--;
                    start = true;
                }
                else if (text == endEvent)
                {
                    events.RemoveAt(i);
                    i--;
                    end = true;
                }
                // Only pass through other events if we're within a phrase
                else if (startTick != null)
                {
                    converter.AddPhraseEvent(text, tick);
                }
            }

            if (start && !end)
            {
                // Unterminated start event
                end = true;
                currentTick = uint.MaxValue;
            }

            // Handle final event state
            if (end)
                ProcessPhraseEvents(converter, currentTick, ref startTick, start, end);
        }

        private static void ProcessPhraseEvents(ITextPhraseConverter converter, uint currentTick,
            ref uint? startTick, bool start, bool end)
        {
            // Phrase starts or ends on this tick
            if (start ^ end)
            {
                if (startTick == null)
                {
                    // Phrase starts on this tick
                    startTick = currentTick;
                }
                else
                {
                    // Phrase ends on this tick
                    converter.AddPhrase(startTick.Value, currentTick, false);
                    // A new one may also start here
                    startTick = start ? currentTick : null;
                }
            }
            else if (start && end)
            {
                if (startTick == null)
                {
                    // Phrase starts and ends on this tick
                    converter.AddPhrase(currentTick, currentTick, false);
                }
                else
                {
                    // Phrase ends on this tick and a new one starts
                    converter.AddPhrase(startTick.Value, currentTick, true);
                    startTick = currentTick;
                }
            }
        }
        #endregion
    }
}