using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private class LyricConverter : ITextPhraseConverter
        {
            private readonly MoonSong _moonSong;

            public readonly List<LyricsPhrase> Phrases;
            private List<LyricEvent> _currentLyrics;

            public string StartEvent => TextEvents.LYRIC_PHRASE_START;
            public string EndEvent => TextEvents.LYRIC_PHRASE_END;

            public LyricConverter(MoonSong song)
            {
                _moonSong = song;
                Phrases = new();
                _currentLyrics = new();
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

                var lyric = text.AsSpan()
                    .Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length).TrimStartAscii();

                // Remove start/end quotes
                lyric.TrimAscii();
                if (!lyric.IsEmpty && lyric[0] == '"')
                    lyric = lyric[1..];
                if (!lyric.IsEmpty && lyric[^1] == '"')
                    lyric = lyric[..^1];
                lyric.TrimAscii();

                var flags = LyricFlags.None;
                string strippedLyric = string.Empty;;
                if (!lyric.IsEmpty)
                {
                    // Handle modifier lyrics
                    char modifier = lyric[^1];
                    if (LyricSymbols.LYRIC_JOIN_SYMBOLS.Contains(modifier))
                        flags |= LyricFlags.JoinWithNext;

                    // Strip special symbols from lyrics
                    strippedLyric = LyricSymbols.StripForLyrics(lyric.ToString());
                }

                if (string.IsNullOrWhiteSpace(strippedLyric))
                {
                    // Allow empty lyrics for lyric gimmick purposes
                    flags |= LyricFlags.JoinWithNext;
                    strippedLyric = string.Empty;
                }

                double time = _moonSong.TickToTime(tick);
                _currentLyrics.Add(new(flags, strippedLyric, time, tick));
            }
        }

        public LyricsTrack LoadLyrics()
        {
            var converter = new LyricConverter(_moonSong);
            TextEvents.ConvertToPhrases(_moonSong.events, converter);
            return new LyricsTrack(converter.Phrases);
        }
    }
}