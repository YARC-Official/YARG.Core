using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Parsing
{
    internal interface ITextPhraseConverter
    {
        string StartEvent { get; }
        string EndEvent { get; }

        void AddPhrase(uint startTick, uint endTick);
        void AddPhraseEvent(string text, uint tick);
    }

    public static partial class TextEvents
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

        #region Crowd State
        public const string
            CROWD_PREFIX = "crowd_",
            CROWD_CLAP  = "crowd_clap",
            CROWD_NOCLAP = "crowd_noclap",
            CROWD_REALTIME = "crowd_realtime",
            CROWD_INTENSE = "crowd_intense",
            CROWD_MELLOW = "crowd_mellow",
            CROWD_NORMAL = "crowd_normal";
        #endregion

        #region Text event to phrase conversion
        private struct TextConversionState
        {
            public uint currentTick;
            public uint? startTick;
            public bool start;
            public bool end;

            // Events pending for the current tick if a start event hasn't occurred yet
            // Cleared on the next tick if no start event is on this tick
            public List<string> pendingEvents;
        }

        internal static void ConvertToPhrases(List<MoonText> events, ITextPhraseConverter converter, uint maxTick)
        {
            string startEvent = converter.StartEvent;
            string endEvent = converter.EndEvent;

            var state = new TextConversionState()
            {
                currentTick = 0,
                startTick = null,
                start = false,
                end = false,
                pendingEvents = new(),
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
                else
                {
                    // Store event as pending for this tick
                    state.pendingEvents.Add(text);
                }
            }

            if (state.start && !state.end)
            {
                // Unterminated start event, we place it at the end of the song
                state.end = true;
                state.currentTick = maxTick + 1;
            }

            // Handle final event state
            if (state.end)
                ProcessPhraseEvents(converter, ref state);
        }

        private static void ProcessPhraseEvents(ITextPhraseConverter converter, ref TextConversionState state)
        {
            // If this is not a start tick...
            if (!state.start)
            {
                // If we are not in any phrase...
                if (state.startTick == null)
                {
                    // Clear all pending events (they must be between phrases)
                    state.pendingEvents.Clear();
                }
                // If we are in a phrase...
                else
                {
                    // Add the events to the phrase.
                    FlushPendingEvents(converter, ref state);
                }
            }

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
                    converter.AddPhrase(state.startTick!.Value, state.currentTick);
                    // A new one may also start here
                    state.startTick = state.start ? state.currentTick : null;
                }
            }
            else if (state.start && state.end)
            {
                if (state.startTick == null)
                {
                    // Phrase starts and ends on this tick
                    converter.AddPhrase(state.currentTick, state.currentTick);
                }
                else
                {
                    // Phrase ends on this tick and a new one starts
                    converter.AddPhrase(state.startTick.Value, state.currentTick);
                    state.startTick = state.currentTick;
                }
            }

            // If a phrase starts here, we still want to flush the events, just after the phrase has begun.
            if (state.startTick != null && state.start)
                FlushPendingEvents(converter, ref state);
        }

        private static void FlushPendingEvents(ITextPhraseConverter converter, ref TextConversionState state)
        {
            foreach (string pending in state.pendingEvents)
            {
                converter.AddPhraseEvent(pending, state.currentTick);
            }
            state.pendingEvents.Clear();
        }
        #endregion
    }
}