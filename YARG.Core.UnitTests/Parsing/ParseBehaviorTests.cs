using MoonscraperChartEditor.Song;
using MoonscraperEngine;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonNote;

    public class ParseBehaviorTests
    {
        public const uint RESOLUTION = 192;
        public const double TEMPO = 120.0;
        public const int NUMERATOR = 4;
        public const int DENOMINATOR = 4;

        public const uint HOPO_THRESHOLD = (uint)(SongConfig.FORCED_NOTE_TICK_THRESHOLD * RESOLUTION / SongConfig.STANDARD_BEAT_RESOLUTION);

        public static readonly SongObjectComparer Comparer = new();

        public static readonly List<SyncTrack> TempoMap = new()
        {
            new BPM(0, (uint)(TEMPO * 1000)),
            new TimeSignature(0, NUMERATOR, DENOMINATOR),
        };

        public static readonly List<Event> GlobalEvents = new()
        {
        };

        private static MoonNote NewNote(GuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)fret, length, flags);
        private static MoonNote NewNote(GHLiveGuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)fret, length, flags);
        private static MoonNote NewNote(DrumPad pad, uint length = 0, Flags flags = Flags.None)
            => new(0, (int)pad, length, flags);
        private static MoonNote NewNote(ProGuitarString str, int fret, uint length = 0, Flags flags = Flags.None)
            => new(0, MoonNote.MakeProGuitarRawNote(str, fret), length, flags);

        public static readonly List<ChartObject> GuitarTrack = new()
        {
            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Blue),
            NewNote(GuitarFret.Orange),
            NewNote(GuitarFret.Open),

            NewNote(GuitarFret.Green, flags: Flags.Forced),
            NewNote(GuitarFret.Red, flags: Flags.Forced),
            NewNote(GuitarFret.Yellow, flags: Flags.Forced),
            NewNote(GuitarFret.Blue, flags: Flags.Forced),
            NewNote(GuitarFret.Orange, flags: Flags.Forced),
            NewNote(GuitarFret.Open, flags: Flags.Forced),

            NewNote(GuitarFret.Green, flags: Flags.Tap),
            NewNote(GuitarFret.Red, flags: Flags.Tap),
            NewNote(GuitarFret.Yellow, flags: Flags.Tap),
            NewNote(GuitarFret.Blue, flags: Flags.Tap),
            NewNote(GuitarFret.Orange, flags: Flags.Tap),

            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Blue),
            NewNote(GuitarFret.Orange),
            NewNote(GuitarFret.Open),

            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Yellow),
            NewNote(GuitarFret.Yellow),

            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
            NewNote(GuitarFret.Green),
            NewNote(GuitarFret.Red),
        };

        public static readonly List<ChartObject> GhlGuitarTrack = new()
        {
            NewNote(GHLiveGuitarFret.Black1),
            NewNote(GHLiveGuitarFret.Black2),
            NewNote(GHLiveGuitarFret.Black3),
            NewNote(GHLiveGuitarFret.White1),
            NewNote(GHLiveGuitarFret.White2),
            NewNote(GHLiveGuitarFret.White3),
            NewNote(GHLiveGuitarFret.Open),

            NewNote(GHLiveGuitarFret.Black1, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Black2, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Black3, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White1, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White2, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.White3, flags: Flags.Forced),
            NewNote(GHLiveGuitarFret.Open, flags: Flags.Forced),

            NewNote(GHLiveGuitarFret.Black1, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.Black2, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.Black3, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White1, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White2, flags: Flags.Tap),
            NewNote(GHLiveGuitarFret.White3, flags: Flags.Tap),

            NewNote(GHLiveGuitarFret.Black1),
            NewNote(GHLiveGuitarFret.Black2),
            NewNote(GHLiveGuitarFret.Black3),
            NewNote(GHLiveGuitarFret.White1),
            NewNote(GHLiveGuitarFret.White2),
            NewNote(GHLiveGuitarFret.White3),
            NewNote(GHLiveGuitarFret.Open),
        };

        public static readonly List<ChartObject> ProGuitarTrack = new()
        {
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Green, 1),
            NewNote(ProGuitarString.Orange, 2),
            NewNote(ProGuitarString.Blue, 3),
            NewNote(ProGuitarString.Yellow, 4),
            NewNote(ProGuitarString.Purple, 5),

            NewNote(ProGuitarString.Red, 6, flags: Flags.Forced),
            NewNote(ProGuitarString.Green, 7, flags: Flags.Forced),
            NewNote(ProGuitarString.Orange, 8, flags: Flags.Forced),
            NewNote(ProGuitarString.Blue, 9, flags: Flags.Forced),
            NewNote(ProGuitarString.Yellow, 10, flags: Flags.Forced),
            NewNote(ProGuitarString.Purple, 11, flags: Flags.Forced),

            NewNote(ProGuitarString.Red, 12, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Green, 13, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Orange, 14, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Blue, 15, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Yellow, 16, flags: Flags.ProGuitar_Muted),
            NewNote(ProGuitarString.Purple, 17, flags: Flags.ProGuitar_Muted),

            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Green, 1),
            NewNote(ProGuitarString.Orange, 2),
            NewNote(ProGuitarString.Blue, 3),
            NewNote(ProGuitarString.Yellow, 4),
            NewNote(ProGuitarString.Purple, 5),

            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Red, 0),
            NewNote(ProGuitarString.Red, 0),

            NewNote(ProGuitarString.Yellow, 5),
            NewNote(ProGuitarString.Yellow, 6),
            NewNote(ProGuitarString.Yellow, 5),
            NewNote(ProGuitarString.Yellow, 6),
            NewNote(ProGuitarString.Yellow, 5),
            NewNote(ProGuitarString.Yellow, 6),
        };

        public static readonly List<ChartObject> DrumsTrack = new()
        {
            NewNote(DrumPad.Kick),
            NewNote(DrumPad.Kick, flags: Flags.DoubleKick),

            NewNote(DrumPad.Red, length: 16),
            NewNote(DrumPad.Yellow, length: 16),
            NewNote(DrumPad.Blue, length: 16),
            NewNote(DrumPad.Orange, length: 16),
            NewNote(DrumPad.Green, length: 16),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Green, flags: Flags.ProDrums_Accent),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),

            NewNote(DrumPad.Red, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Green, flags: Flags.ProDrums_Ghost),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),

            NewNote(DrumPad.Red),
            NewNote(DrumPad.Yellow),
            NewNote(DrumPad.Blue),
            NewNote(DrumPad.Orange),
            NewNote(DrumPad.Green),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red),
            NewNote(DrumPad.Yellow),
            NewNote(DrumPad.Blue),
            NewNote(DrumPad.Orange),
            NewNote(DrumPad.Green),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red),
            NewNote(DrumPad.Yellow),
            NewNote(DrumPad.Blue),
            NewNote(DrumPad.Orange),
            NewNote(DrumPad.Green),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red),
            NewNote(DrumPad.Yellow),
            NewNote(DrumPad.Blue),
            NewNote(DrumPad.Orange),
            NewNote(DrumPad.Green),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(DrumPad.Red),
            NewNote(DrumPad.Red),
            NewNote(DrumPad.Red),
            NewNote(DrumPad.Red),
            NewNote(DrumPad.Red),
            NewNote(DrumPad.Red),

            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
        };

        public static MoonSong GenerateSong()
        {
            var song = new MoonSong();
            PopulateSyncTrack(song, TempoMap);
            PopulateGlobalEvents(song, GlobalEvents);
            PopulateInstrument(song, MoonInstrument.Guitar, GuitarTrack);
            PopulateInstrument(song, MoonInstrument.GHLiveGuitar, GhlGuitarTrack);
            PopulateInstrument(song, MoonInstrument.ProGuitar_22Fret, ProGuitarTrack);
            PopulateInstrument(song, MoonInstrument.Drums, DrumsTrack);
            return song;
        }

        public static void PopulateSyncTrack(MoonSong song, List<SyncTrack> tempoMap)
        {
            foreach (var sync in tempoMap)
            {
                song.Add(sync.Clone(), false);
            }
            song.UpdateCache();
        }

        public static void PopulateGlobalEvents(MoonSong song, List<Event> events)
        {
            foreach (var text in events)
            {
                song.Add(text.Clone(), false);
            }
            song.UpdateCache();
        }

        public static void PopulateInstrument(MoonSong song, MoonInstrument instrument, List<ChartObject> data)
        {
            foreach (var difficulty in EnumX<Difficulty>.Values)
            {
                PopulateDifficulty(song, instrument, difficulty, data);
            }
        }

        public static void PopulateDifficulty(MoonSong song, MoonInstrument instrument, Difficulty difficulty, List<ChartObject> data)
        {
            var chart = song.GetChart(instrument, difficulty);
            uint currentTick = 0;
            foreach (var chartObj in data)
            {
                var newObj = chartObj.Clone();
                newObj.tick = currentTick;
                chart.Add(newObj, false);

                // Only notes will progress the current tick forward, other events
                // will occur at the same position as the last note
                if (newObj.GetType() == typeof(MoonNote))
                    currentTick += RESOLUTION;
            }
            chart.UpdateCache();
        }

        public static void VerifyMetadata(MoonSong sourceSong, MoonSong parsedSong)
        {
            Assert.Multiple(() =>
            {
                Assert.That(parsedSong.resolution, Is.EqualTo(sourceSong.resolution), $"Resolution was not parsed correctly!");
            });
        }

        public static void VerifySync(MoonSong sourceSong, MoonSong parsedSong)
        {
            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(sourceSong.bpms, parsedSong.bpms, Comparer, "BPMs do not match!");
                CollectionAssert.AreEqual(sourceSong.timeSignatures, parsedSong.timeSignatures, Comparer, "Time signatures do not match!");
                CollectionAssert.AreEqual(parsedSong.events, parsedSong.events, Comparer, "Global events do not match!");
            });
        }

        public static void VerifyTrack(MoonSong sourceSong, MoonSong parsedSong, MoonInstrument instrument, Difficulty difficulty)
        {
            Assert.Multiple(() =>
            {
                bool chartExists = parsedSong.DoesChartExist(instrument, difficulty);
                Assert.That(chartExists, Is.True, $"Chart for {difficulty} {instrument} was not parsed!");
                if (!chartExists)
                    return;

                var sourceChart = sourceSong.GetChart(instrument, difficulty);
                var parsedChart = parsedSong.GetChart(instrument, difficulty);
                CollectionAssert.AreEqual(sourceChart.notes, parsedChart.notes, Comparer, $"Notes on {difficulty} {instrument} do not match!");
                CollectionAssert.AreEqual(sourceChart.specialPhrases, parsedChart.specialPhrases, Comparer, $"Special phrases on {difficulty} {instrument} do not match!");
                CollectionAssert.AreEqual(sourceChart.events, parsedChart.events, Comparer, $"Local events on {difficulty} {instrument} do not match!");
            });
        }
    }
}