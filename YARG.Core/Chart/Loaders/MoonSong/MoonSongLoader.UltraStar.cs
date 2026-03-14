using System.IO;
using MoonscraperChartEditor.Song;
using YARG.Core.IO;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public static MoonSongLoader LoadUltraStar(ParseSettings settings, string filePath)
        {
            using var fixedArray = FixedArray.LoadFile(filePath);
            var ultraStarLoader = new UltraStarLoader(fixedArray);
            ultraStarLoader.DumpToLog();
            var moonSong = ConvertUltraStarToMoonSong(ultraStarLoader);
            return new MoonSongLoader(moonSong, settings);
        }

        public static MoonSongLoader LoadUltraStar(ParseSettings settings, byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var fixedArray = FixedArray.Read(ms, bytes.Length);
            var ultraStarLoader = new UltraStarLoader(fixedArray);
            ultraStarLoader.DumpToLog();
            var moonSong = ConvertUltraStarToMoonSong(ultraStarLoader);
            return new MoonSongLoader(moonSong, settings);
        }

        private static MoonSong ConvertUltraStarToMoonSong(UltraStarLoader loader)
        {
            const uint RESOLUTION = 480;
            var moonSong = new MoonSong(RESOLUTION);

            // Sync track
            var syncTrack = loader.LoadSyncTrack();
            foreach (var t in syncTrack.Tempos)
                moonSong.AddTempo(t.BeatsPerMinute, t.Tick);
            foreach (var ts in syncTrack.TimeSignatures)
                moonSong.AddTimeSignature(ts.Numerator, ts.Denominator, ts.Tick);

            // Vocals chart
            var moonChart = moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);
            var vocalTrack = loader.LoadVocalsTrack(Instrument.Vocals);

            foreach (var part in vocalTrack.Parts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    var parent = phrase.PhraseParentNote;

                    moonChart.Add(new MoonPhrase(
                        parent.Tick,
                        parent.TickLength,
                        MoonPhrase.Type.Vocals_ScoringPhrase
                    ));

                    foreach (var lyric in phrase.Lyrics)
                    {
                        moonChart.Add(new MoonText(
                            $"lyric {lyric.Text}",
                            lyric.Tick
                        ));
                    }

                    foreach (var child in parent.ChildNotes)
                    {
                        if (child.Type != VocalNoteType.Lyric) continue;

                        var flags = child.IsNonPitched
                            ? MoonNote.Flags.Vocals_Percussion
                            : MoonNote.Flags.None;

                        int rawNote = child.IsNonPitched ? 0 : (int) child.Pitch;

                        moonChart.Add(new MoonNote(
                            child.Tick,
                            rawNote,
                            child.TickLength,
                            flags
                        ));
                    }
                }
            }

            return moonSong;
        }
    }
}