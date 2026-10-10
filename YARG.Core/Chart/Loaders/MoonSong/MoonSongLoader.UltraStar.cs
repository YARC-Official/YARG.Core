using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        #region Loading

        internal static MoonSongLoader LoadUltraStar(ParseSettings settings, FixedArray<byte> file)
        {
            var ultraStarLoader = new UltraStarLoader(file);
            var moonSong = ConvertUltraStarToMoonSong(ultraStarLoader);

            return new MoonSongLoader(moonSong, settings) { _isUltraStar = true };
        }

        #endregion

        #region Conversion

        private static void AddPartToChart(IEnumerable<VocalsPart> parts, MoonChart chart)
        {
            var allPhrases = new List<MoonPhrase>();
            var allText = new List<MoonText>();
            var allNotes = new List<MoonNote>();

            foreach (var part in parts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    var parent = phrase.PhraseParentNote;

                    allPhrases.Add(new MoonPhrase(parent.Tick, parent.TickLength, MoonPhrase.Type.Vocals_ScoringPhrase));

                    foreach (var lyric in phrase.Lyrics)
                    {
                        allText.Add(new MoonText($"lyric {lyric.Text}", lyric.Tick));
                    }

                    foreach (var child in parent.ChildNotes)
                    {
                        if (child.Type != VocalNoteType.Lyric && child.Type != VocalNoteType.Percussion)
                        {
                            continue;
                        }

                        // UltraStar freestyle (F), rap (R), and golden rap (G) notes are
                        // unpitched lyrical notes, NOT percussion. Keep as None so they stay
                        // VocalNoteType.Lyric downstream.
                        var flags = MoonNote.Flags.None;

                        int rawNote = child.IsNonPitched ? 0 : (int) child.Pitch;

                        allNotes.Add(new MoonNote(child.Tick, rawNote, child.TickLength, flags));
                    }
                }
            }

            var allPhrasesList = new List<MoonPhrase>();

            var lyricPhrases = allPhrases
                .Where(p => p.type == MoonPhrase.Type.Vocals_ScoringPhrase || p.type == MoonPhrase.Type.Vocals_StaticLyricPhrase)
                .GroupBy(p => p.tick)
                .Select(g => g.OrderByDescending(p => p.length).First());
            allPhrasesList.AddRange(lyricPhrases);

            foreach (var part in parts)
            {
                foreach (var phrase in part.OtherPhrases)
                {
                    if (phrase.Type == PhraseType.StarPower)
                    {
                        allPhrasesList.Add(new MoonPhrase(phrase.Tick, phrase.TickLength, MoonPhrase.Type.Starpower));
                    }
                }
            }

            foreach (var p in allPhrasesList.OrderBy(p => p.tick))
            {
                chart.Add(p);
            }

            var uniqueText = allText
                .OrderBy(t => t.tick)
                .GroupBy(t => t.tick)
                .Select(g => g.First());

            foreach (var t in uniqueText)
            {
                chart.Add(t);
            }

            var uniqueNotes = allNotes
                .OrderBy(n => n.tick)
                .ThenByDescending(n => n.length)
                .GroupBy(n => new { n.tick, n.vocalsPitch })
                .Select(g => g.First());

            foreach (var n in uniqueNotes)
            {
                chart.Add(n);
            }
        }

        /// <summary>
        /// SongChart.Lyrics (and the lipsync generated from it) is built from song-level text
        /// events, which a .mid fills from its vocals track (see MidReader) -- add the same
        /// events here, from the part the solo Vocals chart shows.
        /// </summary>
        private static void AddSongLyrics(MoonSong song, VocalsPart part)
        {
            foreach (var phrase in part.NotePhrases)
            {
                var parent = phrase.PhraseParentNote;
                song.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_START, parent.Tick));
                foreach (var lyric in phrase.Lyrics)
                {
                    song.InsertText(new MoonText(TextEvents.LYRIC_PREFIX_WITH_SPACE + lyric.Text, lyric.Tick));
                }
                song.InsertText(new MoonText(TextEvents.LYRIC_PHRASE_END, parent.Tick + parent.TickLength));
            }
        }

        private static MoonSong ConvertUltraStarToMoonSong(UltraStarLoader loader)
        {
            const uint RESOLUTION = 120;
            var moonSong = new MoonSong(RESOLUTION);

            // Sync track
            var syncTrack = loader.LoadSyncTrack();
            foreach (var t in syncTrack.Tempos)
            {
                moonSong.AddTempo(t.BeatsPerMinute, t.Tick);
            }

            foreach (var ts in syncTrack.TimeSignatures)
            {
                moonSong.AddTimeSignature(ts.Numerator, ts.Denominator, ts.Tick);
            }

            bool isMultiVoice = loader.VoiceCount >= 2;

            var vocalTrack = loader.LoadVocalsTrack(isMultiVoice ? Instrument.Harmony : Instrument.Vocals);
            var soloChart = moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            if (vocalTrack.Parts.Count > 0)
            {
                // For multi-voice songs, only show first part in solo Vocals chart.
                // All parts are still available separately in Harmony1/2/3.
                var soloParts = isMultiVoice
                    ? new List<VocalsPart> { vocalTrack.Parts[0] }
                    : vocalTrack.Parts;
                AddPartToChart(soloParts, soloChart);
                AddSongLyrics(moonSong, vocalTrack.Parts[0]);
            }

            if (isMultiVoice)
            {
                var harmonyInstruments = new[]
                {
                    MoonSong.MoonInstrument.Harmony1,
                    MoonSong.MoonInstrument.Harmony2,
                    MoonSong.MoonInstrument.Harmony3,
                };

                for (int i = 0; i < vocalTrack.Parts.Count && i < harmonyInstruments.Length; i++)
                {
                    var chart = moonSong.GetChart(harmonyInstruments[i], MoonSong.Difficulty.Expert);
                    AddPartToChart(new[] { vocalTrack.Parts[i] }, chart);
                }
            }

            return moonSong;
        }

        #endregion
    }
}