using System;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Parsing
{
    /// <summary>
    /// Handles correlation of phrase start/end text events, with correct handling of event ordering.
    /// </summary>
    internal class TextPhraseHandler
    {
        private string _startText;
        private string _endText;
        private bool _startNewOnConsecutiveStart;

        private bool _phraseStart;
        private bool _phraseEnd;

        private uint? _startTick;

        public TextPhraseHandler(string startText, string endText, bool startNewOnConsecutiveStart)
        {
            _startText = startText;
            _endText = endText;
            _startNewOnConsecutiveStart = startNewOnConsecutiveStart;
        }

        /// <param name="deferEventsToNextPhrase">
        /// Whether or not to defer other related events on this tick
        /// to the next phrase that gets created.
        /// </param>
        /// <returns>
        /// True if a phrase has finished, false otherwise.
        /// </returns>
        public bool FinishTick(uint tick, out uint startTick, out bool deferEventsToNextPhrase)
        {
            bool finishedPhrase = false;
            startTick = 0;
            deferEventsToNextPhrase = false;

            // Phrase starts or ends on this tick
            if (_phraseStart ^ _phraseEnd)
            {
                if (_startTick == null)
                {
                    if (!_phraseStart)
                    {
                        YargLogger.LogFormatWarning("Ignoring duplicate '{0}' event", _endText);
                        return false;
                    }

                    // Phrase starts on this tick
                    _startTick = tick;
                }
                else
                {
                    if (!_phraseEnd && !_startNewOnConsecutiveStart)
                    {
                        YargLogger.LogFormatWarning("Ignoring duplicate '{0}' event", _startText);
                        return false;
                    }

                    // Phrase ends on this tick
                    startTick = _startTick.Value;
                    finishedPhrase = true;

                    // Pending events are part of this phrase
                    deferEventsToNextPhrase = false;

                    // A new one may also start here
                    _startTick = _phraseStart ? tick : null;
                }
            }
            else if (_phraseStart && _phraseEnd)
            {
                if (_startTick == null)
                {
                    // Phrase starts and ends on this tick
                    startTick = tick;
                    finishedPhrase = true;

                    // Pending events are part of this phrase
                    deferEventsToNextPhrase = false;
                }
                else
                {
                    // Phrase ends on this tick and a new one starts
                    startTick = _startTick.Value;
                    _startTick = tick;
                    finishedPhrase = true;

                    // Pending events are part of the next phrase
                    deferEventsToNextPhrase = true;
                }
            }

            // Reset start/end flags for the next tick
            _phraseStart = false;
            _phraseEnd = false;

            return finishedPhrase;
        }

        /// <returns>
        /// True if the text event was handled, false otherwise.
        /// </returns>
        public bool ProcessEvent(ReadOnlySpan<char> eventText)
        {
            if (eventText.Equals(_startText, StringComparison.Ordinal))
            {
                if (_phraseStart)
                {
                    YargLogger.LogFormatWarning("Ignoring duplicate '{0}' event", eventText.ToString());
                    return true;
                }

                _phraseStart = true;
            }
            else if (eventText.Equals(_endText, StringComparison.Ordinal))
            {
                if (_phraseEnd)
                {
                    YargLogger.LogFormatWarning("Ignoring duplicate '{0}' event", eventText.ToString());
                    return true;
                }

                _phraseEnd = true;
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}