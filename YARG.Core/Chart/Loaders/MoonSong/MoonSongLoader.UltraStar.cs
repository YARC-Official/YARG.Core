using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Chart.Loaders.UltraStar;
using YARG.Core.IO;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        #region Loading

        public static MoonSongLoader LoadUltraStar(ParseSettings settings, string filePath)
        {
            using var fixedArray = FixedArray.LoadFile(filePath);
            var ultraStarLoader = new UltraStarLoader(fixedArray);
            var moonSong = ConvertUltraStarToMoonSong(ultraStarLoader);

            return new MoonSongLoader(moonSong, settings);
        }

        public static MoonSongLoader LoadUltraStar(ParseSettings settings, byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var fixedArray = FixedArray.Read(ms, bytes.Length);
            var ultraStarLoader = new UltraStarLoader(fixedArray);
            var moonSong = ConvertUltraStarToMoonSong(ultraStarLoader);

            return new MoonSongLoader(moonSong, settings);
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
                        if (child.Type != VocalNoteType.Lyric)
                        {
                            continue;
                        }

                        var flags = child.IsNonPitched ? MoonNote.Flags.Vocals_Percussion : MoonNote.Flags.None;

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

            bool isDuet = loader.GetMetadata("PARTS") == "2";

            var vocalTrack = loader.LoadVocalsTrack(isDuet ? Instrument.Harmony : Instrument.Vocals);
            var soloChart = moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            if (vocalTrack.Parts.Count > 0)
            {
                AddPartToChart(vocalTrack.Parts, soloChart);
            }

            if (isDuet && vocalTrack.Parts.Count >= 2)
            {
                var chartH1 = moonSong.GetChart(MoonSong.MoonInstrument.Harmony1, MoonSong.Difficulty.Expert);
                AddPartToChart(new[] { vocalTrack.Parts[0] }, chartH1);

                var chartH2 = moonSong.GetChart(MoonSong.MoonInstrument.Harmony2, MoonSong.Difficulty.Expert);
                AddPartToChart(new[] { vocalTrack.Parts[1] }, chartH2);
            }

            return moonSong;
        }

        #endregion
    }
}