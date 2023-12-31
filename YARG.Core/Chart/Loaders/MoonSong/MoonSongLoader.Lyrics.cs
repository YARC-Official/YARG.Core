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

                var lyric = text.AsSpan().Slice(TextEvents.LYRIC_PREFIX_WITH_SPACE.Length);

                // Workaround for a certain set of badly-formatted vocal tracks which place the hyphen
                // for pitch bend lyrics on the pitch bend and not the lyric itself
                if (_currentLyrics.Count > 0 && !_currentLyrics[^1].JoinWithNext &&
                    (lyric.Equals("+-", StringComparison.Ordinal) || lyric.Equals("-+", StringComparison.Ordinal)))
                {
                    var other = _currentLyrics[^1];
                    _currentLyrics[^1] = new(LyricSymbolFlags.JoinWithNext, other.Text, other.Time, other.Tick);
                    return;
                }

                // Handle lyric modifiers
                var flags = LyricSymbols.GetLyricFlags(lyric);

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

        public LyricsTrack LoadLyrics()
        {
            var converter = new LyricConverter(_moonSong);
            TextEvents.ConvertToPhrases(_moonSong.events, converter);
            return new LyricsTrack(converter.Phrases);
        }
    }
}