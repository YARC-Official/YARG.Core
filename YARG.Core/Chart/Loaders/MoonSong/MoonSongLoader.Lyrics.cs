using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private class LyricConverter : ITextPhraseConverter
        {
            private readonly MoonSong _moonSong;
            private readonly List<MoonPhrase> _censorPhrases;

            public readonly List<LyricsPhrase> Phrases;
            private List<LyricEvent> _currentLyrics;

            public string StartEvent => TextEvents.LYRIC_PHRASE_START;
            public string EndEvent => TextEvents.LYRIC_PHRASE_END;

            public LyricConverter(MoonSong song)
            {
                _moonSong = song;
                Phrases = new();
                _currentLyrics = new();
                _censorPhrases = _moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert)
                    ?.specialPhrases
                    .Where(x => x.type == MoonPhrase.Type.Vocals_Censorship)
                    .ToList() ?? new();
            }

            public bool IsCensorableAtTick(uint tick)
            {
                if (_censorPhrases.Count == 0)
                {
                    return false;
                }

                return _censorPhrases.Any(x => (x.tick <= tick) && (x.tick + x.length > tick));
            }

            public void AddPhrase(uint startTick, uint endTick)
            {
                if (_currentLyrics.Count < 1)
                    return;

                double startTime = _moonSong.TickToTime(startTick);
                double endTime = _moonSong.TickToTime(endTick);
                Phrases.Add(new(startTime, endTime - startTime, startTick, endTick - startTick, _currentLyrics));
                _currentLyrics = new();
            }

            public void AddPhraseEvent(string text, uint tick)
            {
                // Ignore non-lyric events
                if (!text.StartsWith(TextEvents.LYRIC_PREFIX_WITH_SPACE))
                    return;

                var lyric = text.AsSpan().Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length);

                LyricSymbols.DeferredLyricJoinWorkaround(_currentLyrics, ref lyric, false);

                // Handle lyric modifiers
                var flags = LyricSymbols.GetLyricFlags(lyric);

                if (IsCensorableAtTick(tick))
                {
                    flags |= LyricSymbolFlags.Censorable;
                }

                // Strip special symbols from lyrics
                string strippedLyric = !lyric.IsEmpty
                    ? LyricSymbols.StripForLyrics(lyric.ToString())
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(strippedLyric))
                {
                    // Allow empty lyrics for lyric gimmick purposes
                    flags |= LyricSymbolFlags.JoinWithNext;
                    strippedLyric = string.Empty;
                }

                double time = _moonSong.TickToTime(tick);
                _currentLyrics.Add(new(flags, strippedLyric, time, tick));
            }
        }
        private static void ApplyCensoringToLyricEvents(List<LyricEvent> lyricEvents)
        {
            LyricEvent? firstInWord = null;
            int i = 0;
            while (i < lyricEvents.Count)
            {
                var lyric = lyricEvents[i];
                if (lyric.Censorable)
                {
                    if (firstInWord != null)
                    {
                        // This syllable is part of a censored word — remove it.
                        // If it also joins/hyphens with the next, keep removing.
                        YargLogger.LogFormatTrace(
                            "Removing lyric \"{0}\" at tick {1} due to preceding lyric being censored",
                            lyric.Text, lyric.Tick);
                        lyricEvents.RemoveAt(i);
                        firstInWord.TickLength += lyric.TickLength;
                        firstInWord.TimeLength += lyric.TimeLength;
                        firstInWord = lyric.JoinOrHyphenateWithNext ? firstInWord : null;
                        // Don't increment i — the next element has shifted into position i.
                    }
                    else
                    {
                        // First syllable of a censored word — replace with a dash.
                        // If it joins/hyphens with the next, those need to be removed too.
                        YargLogger.LogFormatTrace(
                            "Censoring lyric \"{0}\" at tick {1}", lyric.Text, lyric.Tick);
                        lyricEvents[i] = new LyricEvent(lyric.Flags & ~LyricSymbolFlags.HyphenateWithNext & ~LyricSymbolFlags.JoinWithNext, "-", lyric.Time, lyric.Tick);
                        firstInWord = lyricEvents[i];
                        i++;
                    }
                }
                else
                {
                    // Not censorable — if removeNext was set, that chain ended at a
                    // non-censorable lyric, which shouldn't happen in well-formed data,
                    // but reset it defensively.
                    firstInWord = null;
                    i++;
                }
            }
        }

        public LyricsTrack LoadLyrics()
        {
            var converter = new LyricConverter(_moonSong);
            var maxTick = _moonSong.Charts.Max(x => x.events.LastOrDefault()?.tick + _moonSong.resolution ?? 0);
            TextEvents.ConvertToPhrases(_moonSong.events, converter, maxTick);
            if (_settings.CensoringEnabled)
            {
                foreach (var phrase in converter.Phrases)
                {
                    ApplyCensoringToLyricEvents(phrase.Lyrics);
                }
            }
            return new LyricsTrack(converter.Phrases);
        }
    }
}