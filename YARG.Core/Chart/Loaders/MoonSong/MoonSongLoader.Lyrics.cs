using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private class LyricConverter : ITextPhraseConverter
        {
            private readonly MoonSong _moonSong;
            private readonly List<MoonNote> _censoredNotes;
            private int _censorNoteIndex;

            public readonly List<LyricsPhrase> Phrases;
            private List<LyricEvent> _currentLyrics;

            public string StartEvent => TextEvents.LYRIC_PHRASE_START;
            public string EndEvent => TextEvents.LYRIC_PHRASE_END;

            public LyricConverter(MoonSong song)
            {
                _moonSong = song;
                Phrases = new();
                _currentLyrics = new();
                _censoredNotes = _moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert).notes.Where(n => n.flags.HasFlag(MoonNote.Flags.Vocals_Censorship)).ToList();
                _censorNoteIndex = 0;
            }

            private bool IsCensorableAtTick(uint tick)
            {
                // Check if the current censor note is before the tick
                while (_censorNoteIndex < _censoredNotes.Count && _censoredNotes[_censorNoteIndex].tick + _censoredNotes[_censorNoteIndex].length <= tick)
                {
                    _censorNoteIndex++;
                }

                // Check if the current censor note is at the tick
                if (_censorNoteIndex < _censoredNotes.Count)
                {
                    var censorNote = _censoredNotes[_censorNoteIndex];
                    if (censorNote.tick == tick)
                    {
                        return true;
                    }
                }

                return false;
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
                _currentLyrics.Add(new(flags, strippedLyric, time, tick, IsCensorableAtTick(tick)));
            }
        }

        public LyricsTrack LoadLyrics()
        {
            var converter = new LyricConverter(_moonSong);
            var maxTick = _moonSong.Charts.Max(x => x.events.LastOrDefault()?.tick + _moonSong.resolution ?? 0);
            TextEvents.ConvertToPhrases(_moonSong.events, converter, maxTick);
            return new LyricsTrack(converter.Phrases);
        }
    }
}