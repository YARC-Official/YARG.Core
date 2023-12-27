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
        private struct TextConversionState
        {
            public uint currentTick;
            public uint? startTick;
            public bool start;
            public bool end;
        }

        public static void ConvertToPhrases(List<MoonText> events, ITextPhraseConverter converter)
        {
            string startEvent = converter.StartEvent;
            string endEvent = converter.EndEvent;

            var state = new TextConversionState()
            {
                currentTick = 0,
                startTick = null,
                start = false,
                end = false,
            };

            for (int i = 0; i < events.Count; i++)
            {
                var textEv = events[i];
                uint tick = textEv.tick;
                string text = textEv.text;

                // Commit found events on start of next tick
                if (tick != state.currentTick)
                {
                    ProcessPhraseEvents(converter, ref state);
                    state.currentTick = tick;
                    state.start = state.end = false;
                }

                // Determine what events are present on the current tick
                if (text == startEvent)
                {
                    events.RemoveAt(i);
                    i--;
                    state.start = true;
                }
                else if (text == endEvent)
                {
                    events.RemoveAt(i);
                    i--;
                    state.end = true;
                }
                // Only pass through other events if we're within a phrase
                else if (state.startTick != null)
                {
                    converter.AddPhraseEvent(text, tick);
                }
            }

            if (state.start && !state.end)
            {
                // Unterminated start event
                state.end = true;
                state.currentTick = uint.MaxValue;
            }

            // Handle final event state
            if (state.end)
                ProcessPhraseEvents(converter, ref state);
        }

        private static void ProcessPhraseEvents(ITextPhraseConverter converter, ref TextConversionState state)
        {
            // Phrase starts or ends on this tick
            if (state.start ^ state.end)
            {
                if (state.startTick == null)
                {
                    // Phrase starts on this tick
                    state.startTick = state.currentTick;
                }
                else
                {
                    // Phrase ends on this tick
                    converter.AddPhrase(state.startTick.Value, state.currentTick, false);
                    // A new one may also start here
                    state.startTick = state.start ? state.currentTick : null;
                }
            }
            else if (state.start && state.end)
            {
                if (state.startTick == null)
                {
                    // Phrase starts and ends on this tick
                    converter.AddPhrase(state.currentTick, state.currentTick, false);
                }
                else
                {
                    // Phrase ends on this tick and a new one starts
                    converter.AddPhrase(state.startTick.Value, state.currentTick, true);
                    state.startTick = state.currentTick;
                }
            }
        }
        #endregion
    }
}