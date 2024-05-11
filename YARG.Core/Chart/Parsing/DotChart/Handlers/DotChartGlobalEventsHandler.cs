using System;
using System.Collections.Generic;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.Chart.Parsing
{
    internal class DotChartGlobalEventsHandler : DotChartSectionHandler
    {
        private bool _endEvent;

        private string? _pendingSection = null;

        private (LyricSymbolFlags flags, string text)? _pendingLyric = null;
        private TextPhraseHandler _lyricPhraser = new(
            DotChartTextEvents.LYRICS_PHRASE_START, DotChartTextEvents.LYRICS_PHRASE_END, true);
        private List<LyricEvent> _currentLyrics = new();

        // TODO: Venue events

        public DotChartGlobalEventsHandler(SongChart chart) : base(chart) {}

        protected override void FinishTick(uint tick)
        {
            FinishEndEvent(tick);
            FinishSection(tick);
            FinishLyrics(tick);
        }

        private void FinishEndEvent(uint tick)
        {
            if (_endEvent)
            {
                _chart.EndMarkerTick = tick;
                _endEvent = false;
            }
        }

        private void FinishSection(uint tick)
        {
            if (_pendingSection != null)
            {
                _chart.Sections.Add(new(_pendingSection, TickToTime(tick), tick));
                _pendingSection = null;
            }
        }

        private void FinishLyrics(uint tick)
        {
            if (_lyricPhraser.FinishTick(tick, out uint startTick, out bool deferEvents))
            {
                if (!deferEvents)
                {
                    // Commit events from this tick now so that they're included in the new phrase
                    FinishLyricEvent(tick);
                }

                double startTime = TickToTime(startTick);
                double endTime = TickToTime(tick);
                _chart.Lyrics.Phrases.Add(new(startTime, endTime - startTime, startTick, tick - startTick, _currentLyrics));
                _currentLyrics = new();
            }

            FinishLyricEvent(tick);
        }

        private void FinishLyricEvent(uint tick)
        {
            if (_pendingLyric is {} lyric)
            {
                _currentLyrics.Add(new(lyric.flags, lyric.text, TickToTime(tick), tick));
                _pendingLyric = null;
            }
        }

        protected override bool ProcessEvent(ReadOnlySpan<char> typeText, ReadOnlySpan<char> eventText)
        {
            if (typeText.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                eventText = TextEvents.NormalizeTextEvent(eventText.TrimOnce('"'));

                if (TextEvents.TryParseSectionEvent(eventText, out var sectionName))
                {
                    if (_pendingSection != null)
                    {
                        YargLogger.LogFormatWarning("Ignoring duplicate section name '{0}'", sectionName.ToString());
                        return true;
                    }

                    _pendingSection = sectionName.ToString();
                }
                else if (DotChartTextEvents.TryParseLyricEvent(eventText, out var lyricText))
                {
                    if (_pendingLyric != null)
                    {
                        YargLogger.LogFormatWarning("Ignoring duplicate lyric event '{0}'", lyricText.ToString());
                        return true;
                    }

                    LyricSymbols.DeferredLyricJoinWorkaround(_currentLyrics, ref lyricText, false);

                    // Handle lyric modifiers
                    var flags = LyricSymbols.GetLyricFlags(lyricText);

                    // Strip special symbols from lyrics
                    string strippedLyric = !lyricText.IsEmpty
                        ? LyricSymbols.StripForLyrics(lyricText.ToString())
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(strippedLyric))
                    {
                        // Allow empty lyrics for lyric gimmick purposes
                        flags |= LyricSymbolFlags.JoinWithNext;
                        strippedLyric = string.Empty;
                    }

                    _pendingLyric = (flags, strippedLyric);
                }
                else if (eventText.Equals(TextEvents.END_MARKER, StringComparison.Ordinal))
                {
                    _endEvent = true;
                }
                else
                {
                    return _lyricPhraser.ProcessEvent(eventText);
                }

                return true;
            }
            else if (typeText.Equals("H", StringComparison.OrdinalIgnoreCase))
            {
                // This event is used for the guitarist hand position:
                // H <position from 0-19> <length>
                // GH1 put them on a global ANIM track, and so they got
                // thrown into the global [Events] section in the early days of .chart
                // We don't currently use hand position info, so this is left unparsed
                return true;
            }

            return false;
        }
    }
}