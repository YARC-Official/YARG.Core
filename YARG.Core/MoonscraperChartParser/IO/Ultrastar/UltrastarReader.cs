using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Core.Utility;

namespace YARG.Core.MoonscraperChartParser.IO.Ultrastar
{
    internal static partial class UltrastarReader
    {
        private const int MIDI_OFFSET_C4 = 60;

        /// <summary>
        /// BPM in Ultrastar file is 1/4th of the original, so multiplying to get correct bpm.
        /// </summary>
        private const float BPM_MULTIPLIER = 4f;

        private const char SLIDE_CHAR = '~';

        private enum NoteType
        {
            Normal,
            Freestyle,
            Rap
        }

        private struct ChartSettings
        {
            public ChartSettings(uint _gap, uint _player, float _bpm, bool _relativeOffsets, bool _reachedEnd)
            {
                gapInMilliseconds = _gap;
                currentPlayer = _player;
                bpm = _bpm;
                relativeNoteOffsets = _relativeOffsets;
                reachedEnd = _reachedEnd;
            }

            public uint gapInMilliseconds;
            public uint currentPlayer;
            public float bpm;
            public bool relativeNoteOffsets;
            public bool reachedEnd;
        }

        private const uint DEFAULT_RESOLUTION = 480;

        public static MoonSong ReadFromFile(string filepath)
        {
            var settings = ParseSettings.Default_Chart;
            return ReadFromFile(ref settings, filepath);
        }

        public static MoonSong ReadFromText(ReadOnlySpan<char> chartText)
        {
            var settings = ParseSettings.Default_Chart;
            return ReadFromText(ref settings, chartText);
        }

        public static MoonSong ReadFromFile(ref ParseSettings settings, string filepath)
        {
            try
            {
                if (!File.Exists(filepath)) throw new Exception("File does not exist");

                string extension = Path.GetExtension(filepath);

                if (extension != ".txt") throw new Exception("Bad file type");

                string text = File.ReadAllText(filepath);
                return ReadFromText(ref settings, text);
            }
            catch (Exception e)
            {
                throw new Exception("Could not open file!", e);
            }
        }

        public static MoonSong ReadFromText(ref ParseSettings settings, ReadOnlySpan<char> chartText)
        {
            int textIndex = 0;

            ChartSettings chartSettings = new ChartSettings(0, 0, 120f, false, false);
            var song = new MoonSong(DEFAULT_RESOLUTION);
            var chart = song.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            ReadOnlySpan<char> line;
            chart.specialPhrases.Add(new MoonPhrase(0, 0, MoonPhrase.Type.Vocals_LyricPhrase));

            while ((line = GetNextLine(chartText, ref textIndex)).Length > 0 && !chartSettings.reachedEnd)
            {
                ParseLine(line, ref song, ref chartSettings);

                textIndex += 1;
            }

            // Fix last special phrase end time
            {
                uint endOfLastNote = chart.notes.Last().tick + chart.notes.Last().length;
                chart.specialPhrases.Last().length = endOfLastNote - chart.specialPhrases.Last().tick;
            }

            return song;
        }

        private static void ParseLine(ReadOnlySpan<char> lineText, ref MoonSong song, ref ChartSettings chartSettings)
        {
            if (lineText.Length < 1)
            {
                return;
            }

            switch (lineText[0])
            {
                case '#':
                    ParseMetadataLine(lineText, ref chartSettings, ref song);
                    break;
                case 'P':
                    ParsePlayerLine(lineText, ref chartSettings);
                    break;
                case ':':
                case '*':
                case 'F':
                case 'R':
                case 'G':
                    ParseNoteLine(lineText, chartSettings, ref song);
                    break;
                case '-':
                    ParsePhraseEndLine(lineText, chartSettings, ref song);
                    break;
                case 'E':
                    chartSettings.reachedEnd = true;
                    break;
            }
        }

        private static void ParseMetadataLine(ReadOnlySpan<char> lineText, ref ChartSettings chartSettings, ref MoonSong song)
        {
            var splits = lineText.Split(':');
            splits.MoveNext();

            if (splits.Current.StartsWith("#BPM"))
            {
                if (splits.MoveNext())
                {
                    if (float.TryParse(splits.Current, out float bpm))
                    {
                        song.syncTrack.Tempos.Clear();
                        var tempoChange = new TempoChange(bpm * BPM_MULTIPLIER, 0, 0);
                        song.syncTrack.Tempos.Add(tempoChange);
                        chartSettings.bpm = bpm * BPM_MULTIPLIER;
                    }
                }
            }

            if (splits.Current.StartsWith("#GAP") || splits.Current.StartsWith("#AUDIOGAP"))
            {
                if (splits.MoveNext())
                {
                    if (uint.TryParse(splits.Current, out uint gap))
                    {
                        chartSettings.gapInMilliseconds = gap;
                    }
                }
            }
        }

        private static void ParsePlayerLine(ReadOnlySpan<char> lineText, ref ChartSettings chartSettings)
        {
            if (lineText.Length < 2)
            {
                return;
            }

            if (uint.TryParse(lineText[1].ToString(), out uint playerId))
            {
                chartSettings.currentPlayer = playerId - 1;
            }
        }

        private static void ParseNoteLine(ReadOnlySpan<char> lineText, ChartSettings chartSettings, ref MoonSong song)
        {
            var chart = song.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            var splits = lineText.Split(' ');
            splits.MoveNext();
            splits.MoveNext();

            NoteType type = lineText[0] switch
                {
                    ':' or '*' => NoteType.Normal,
                    'F' => NoteType.Freestyle,
                    'R' or 'G' => NoteType.Rap,
                    _ => NoteType.Normal
                };

            bool isGolden = lineText[0] == '*' || lineText[0] == 'G';

            uint.TryParse(splits.Current, out uint startBeat);
            splits.MoveNext();
            uint.TryParse(splits.Current, out uint lengthInBeats);
            splits.MoveNext();
            int.TryParse(splits.Current, out int pitch);
            splits.MoveNext();

            int splitIndex = splits.Original.Length - (splits.Current.Length + splits.Remaining.Length) - 1;
            string text = splits.Original[splitIndex..].ToString();

            float timeOfStartBeat = GetTimeOfBeat(chartSettings, startBeat);
            uint startTick = song.TimeToTick(timeOfStartBeat);

            float timeOfSustainEnd = GetTimeOfBeat(chartSettings, startBeat + lengthInBeats);
            uint sustainTicks = song.TimeToTick(timeOfSustainEnd) - startTick;

            var note = new MoonNote(startTick, pitch + MIDI_OFFSET_C4, sustainTicks);
            MoonObjectHelper.PushNote(note, chart.notes);

            string lyricModifier = type switch
            {
                NoteType.Rap => LyricSymbols.NONPITCHED_SYMBOL.ToString(),
                _ => ""
            };

            if (text.Length > 0 && text[0] == SLIDE_CHAR)
            {
                lyricModifier += LyricSymbols.PITCH_SLIDE_SYMBOL;
                text = text[1..];
            }

            string lyricEvent = TextEvents.LYRIC_PREFIX_WITH_SPACE + text + lyricModifier;
            chart.events.Add(new MoonText(lyricEvent, startTick));

            if (chart.specialPhrases.Count == 1 && chart.specialPhrases.First().tick == 0)
            {
                chart.specialPhrases.First().tick = startTick;
            }
        }

        private static void ParsePhraseEndLine(ReadOnlySpan<char> lineText, ChartSettings chartSettings, ref MoonSong song)
        {
            var splits = lineText.Split(' ');
            splits.MoveNext();
            splits.MoveNext();

            var chart = song.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            if (uint.TryParse(splits.Current, out uint nextPhraseStartBeat))
            {
                float timeOfNextPhraseStart = GetTimeOfBeat(chartSettings, nextPhraseStartBeat);
                uint tickOfNextPhraseStart = song.TimeToTick(timeOfNextPhraseStart);

                var lastPhrase = chart.specialPhrases.Last();
                lastPhrase.length = tickOfNextPhraseStart - lastPhrase.tick;
                
                var nextPhrase = new MoonPhrase(tickOfNextPhraseStart, 0, MoonPhrase.Type.Vocals_LyricPhrase);
                chart.specialPhrases.Add(nextPhrase);
            }
        }

        private static float GetTimeOfBeat(ChartSettings chartSettings, uint beat)
        {
            float gapSeconds = chartSettings.gapInMilliseconds / 1000f;
            float secondsPerBeat = 60 / chartSettings.bpm;
            return gapSeconds + (secondsPerBeat * beat);
        }

        private static ReadOnlySpan<char> GetNextLine(ReadOnlySpan<char> chartText, ref int textIndex)
        {
            int startOffset = textIndex;

            if (startOffset >= chartText.Length)
                return ReadOnlySpan<char>.Empty;

            int nextLineBreakOffset = chartText[startOffset..].IndexOfAny('\r', '\n');

            if (nextLineBreakOffset == -1)
            {
                textIndex = chartText.Length;
                return chartText[startOffset..];
            }

            textIndex = startOffset + nextLineBreakOffset;

            if (textIndex + 1 < chartText.Length && chartText[textIndex] == '\r' && chartText[textIndex + 1] == '\n')
            {
                textIndex++;
            }

            return chartText.Slice(startOffset, nextLineBreakOffset);
        }
    }
}
