using MoonscraperChartEditor.Song;
using MoonscraperEngine;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
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

        private static MoonNote NewNote(int index, GuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new((uint)(index * RESOLUTION), (int)fret, length, flags);
        private static MoonNote NewNote(int index, GHLiveGuitarFret fret, uint length = 0, Flags flags = Flags.None)
            => new((uint)(index * RESOLUTION), (int)fret, length, flags);
        private static MoonNote NewNote(int index, DrumPad pad, uint length = 0, Flags flags = Flags.None)
            => new((uint)(index * RESOLUTION), (int)pad, length, flags);
        private static MoonNote NewNote(int index, ProGuitarString str, int fret, uint length = 0, Flags flags = Flags.None)
            => new((uint)(index * RESOLUTION), MoonNote.MakeProGuitarRawNote(str, fret), length, flags);
        private static SpecialPhrase NewSpecial(int index, SpecialPhrase.Type type, uint length = 0)
            => new((uint)(index * RESOLUTION), length, type);

        public static readonly List<ChartObject> GuitarTrack = new()
        {
            NewNote(0, GuitarFret.Green),
            NewNote(1, GuitarFret.Red),
            NewNote(2, GuitarFret.Yellow),
            NewNote(3, GuitarFret.Blue),
            NewNote(4, GuitarFret.Orange),
            NewNote(5, GuitarFret.Open),

            NewSpecial(6, SpecialPhrase.Type.Versus_Player1, RESOLUTION * 6),
            NewNote(6, GuitarFret.Green, flags: Flags.Forced),
            NewNote(7, GuitarFret.Red, flags: Flags.Forced),
            NewNote(8, GuitarFret.Yellow, flags: Flags.Forced),
            NewNote(9, GuitarFret.Blue, flags: Flags.Forced),
            NewNote(10, GuitarFret.Orange, flags: Flags.Forced),
            NewNote(11, GuitarFret.Open, flags: Flags.Forced),

            NewSpecial(12, SpecialPhrase.Type.Versus_Player2, RESOLUTION * 5),
            NewNote(12, GuitarFret.Green, flags: Flags.Tap),
            NewNote(13, GuitarFret.Red, flags: Flags.Tap),
            NewNote(14, GuitarFret.Yellow, flags: Flags.Tap),
            NewNote(15, GuitarFret.Blue, flags: Flags.Tap),
            NewNote(16, GuitarFret.Orange, flags: Flags.Tap),

            NewSpecial(17, SpecialPhrase.Type.Starpower, RESOLUTION * 6),
            NewNote(17, GuitarFret.Green),
            NewNote(18, GuitarFret.Red),
            NewNote(19, GuitarFret.Yellow),
            NewNote(20, GuitarFret.Blue),
            NewNote(21, GuitarFret.Orange),
            NewNote(22, GuitarFret.Open),

            NewSpecial(23, SpecialPhrase.Type.TremoloLane, RESOLUTION * 6),
            NewNote(23, GuitarFret.Yellow),
            NewNote(24, GuitarFret.Yellow),
            NewNote(25, GuitarFret.Yellow),
            NewNote(26, GuitarFret.Yellow),
            NewNote(27, GuitarFret.Yellow),
            NewNote(28, GuitarFret.Yellow),

            NewSpecial(29, SpecialPhrase.Type.TrillLane, RESOLUTION * 6),
            NewNote(29, GuitarFret.Green),
            NewNote(30, GuitarFret.Red),
            NewNote(31, GuitarFret.Green),
            NewNote(32, GuitarFret.Red),
            NewNote(33, GuitarFret.Green),
            NewNote(34, GuitarFret.Red),
        };

        public static readonly List<ChartObject> GhlGuitarTrack = new()
        {
            NewNote(0, GHLiveGuitarFret.Black1),
            NewNote(1, GHLiveGuitarFret.Black2),
            NewNote(2, GHLiveGuitarFret.Black3),
            NewNote(3, GHLiveGuitarFret.White1),
            NewNote(4, GHLiveGuitarFret.White2),
            NewNote(5, GHLiveGuitarFret.White3),
            NewNote(6, GHLiveGuitarFret.Open),

            NewNote(7, GHLiveGuitarFret.Black1, flags: Flags.Forced),
            NewNote(8, GHLiveGuitarFret.Black2, flags: Flags.Forced),
            NewNote(9, GHLiveGuitarFret.Black3, flags: Flags.Forced),
            NewNote(10, GHLiveGuitarFret.White1, flags: Flags.Forced),
            NewNote(11, GHLiveGuitarFret.White2, flags: Flags.Forced),
            NewNote(12, GHLiveGuitarFret.White3, flags: Flags.Forced),
            NewNote(13, GHLiveGuitarFret.Open, flags: Flags.Forced),

            NewNote(14, GHLiveGuitarFret.Black1, flags: Flags.Tap),
            NewNote(15, GHLiveGuitarFret.Black2, flags: Flags.Tap),
            NewNote(16, GHLiveGuitarFret.Black3, flags: Flags.Tap),
            NewNote(17, GHLiveGuitarFret.White1, flags: Flags.Tap),
            NewNote(18, GHLiveGuitarFret.White2, flags: Flags.Tap),
            NewNote(19, GHLiveGuitarFret.White3, flags: Flags.Tap),

            NewSpecial(20, SpecialPhrase.Type.Starpower, RESOLUTION * 7),
            NewNote(20, GHLiveGuitarFret.Black1),
            NewNote(21, GHLiveGuitarFret.Black2),
            NewNote(22, GHLiveGuitarFret.Black3),
            NewNote(23, GHLiveGuitarFret.White1),
            NewNote(24, GHLiveGuitarFret.White2),
            NewNote(25, GHLiveGuitarFret.White3),
            NewNote(26, GHLiveGuitarFret.Open),
        };

        public static readonly List<ChartObject> ProGuitarTrack = new()
        {
            NewNote(0, ProGuitarString.Red, 0),
            NewNote(1, ProGuitarString.Green, 1),
            NewNote(2, ProGuitarString.Orange, 2),
            NewNote(3, ProGuitarString.Blue, 3),
            NewNote(4, ProGuitarString.Yellow, 4),
            NewNote(5, ProGuitarString.Purple, 5),

            NewNote(6, ProGuitarString.Red, 6, flags: Flags.Forced),
            NewNote(7, ProGuitarString.Green, 7, flags: Flags.Forced),
            NewNote(8, ProGuitarString.Orange, 8, flags: Flags.Forced),
            NewNote(9, ProGuitarString.Blue, 9, flags: Flags.Forced),
            NewNote(10, ProGuitarString.Yellow, 10, flags: Flags.Forced),
            NewNote(11, ProGuitarString.Purple, 11, flags: Flags.Forced),

            NewNote(12, ProGuitarString.Red, 12, flags: Flags.ProGuitar_Muted),
            NewNote(13, ProGuitarString.Green, 13, flags: Flags.ProGuitar_Muted),
            NewNote(14, ProGuitarString.Orange, 14, flags: Flags.ProGuitar_Muted),
            NewNote(15, ProGuitarString.Blue, 15, flags: Flags.ProGuitar_Muted),
            NewNote(16, ProGuitarString.Yellow, 16, flags: Flags.ProGuitar_Muted),
            NewNote(17, ProGuitarString.Purple, 17, flags: Flags.ProGuitar_Muted),

            NewSpecial(18, SpecialPhrase.Type.Starpower, RESOLUTION * 6),
            NewNote(18, ProGuitarString.Red, 0),
            NewNote(19, ProGuitarString.Green, 1),
            NewNote(20, ProGuitarString.Orange, 2),
            NewNote(21, ProGuitarString.Blue, 3),
            NewNote(22, ProGuitarString.Yellow, 4),
            NewNote(23, ProGuitarString.Purple, 5),

            NewSpecial(24, SpecialPhrase.Type.TremoloLane, RESOLUTION * 6),
            NewNote(24, ProGuitarString.Red, 0),
            NewNote(25, ProGuitarString.Red, 0),
            NewNote(26, ProGuitarString.Red, 0),
            NewNote(27, ProGuitarString.Red, 0),
            NewNote(28, ProGuitarString.Red, 0),
            NewNote(29, ProGuitarString.Red, 0),

            NewSpecial(30, SpecialPhrase.Type.TrillLane, RESOLUTION * 6),
            NewNote(30, ProGuitarString.Yellow, 5),
            NewNote(31, ProGuitarString.Yellow, 6),
            NewNote(32, ProGuitarString.Yellow, 5),
            NewNote(33, ProGuitarString.Yellow, 6),
            NewNote(34, ProGuitarString.Yellow, 5),
            NewNote(35, ProGuitarString.Yellow, 6),
        };

        public static readonly List<ChartObject> DrumsTrack = new()
        {
            NewNote(0, DrumPad.Kick),
            NewNote(1, DrumPad.Kick, flags: Flags.DoubleKick),

            NewNote(2, DrumPad.Red, length: 16),
            NewNote(3, DrumPad.Yellow, length: 16),
            NewNote(4, DrumPad.Blue, length: 16),
            NewNote(5, DrumPad.Orange, length: 16),
            NewNote(6, DrumPad.Green, length: 16),
            NewNote(7, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(8, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(9, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewNote(10, DrumPad.Red, flags: Flags.ProDrums_Accent),
            NewNote(11, DrumPad.Yellow, flags: Flags.ProDrums_Accent),
            NewNote(12, DrumPad.Blue, flags: Flags.ProDrums_Accent),
            NewNote(13, DrumPad.Orange, flags: Flags.ProDrums_Accent),
            NewNote(14, DrumPad.Green, flags: Flags.ProDrums_Accent),
            NewNote(15, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(16, DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),
            NewNote(17, DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Accent),

            NewNote(18, DrumPad.Red, flags: Flags.ProDrums_Ghost),
            NewNote(19, DrumPad.Yellow, flags: Flags.ProDrums_Ghost),
            NewNote(20, DrumPad.Blue, flags: Flags.ProDrums_Ghost),
            NewNote(21, DrumPad.Orange, flags: Flags.ProDrums_Ghost),
            NewNote(22, DrumPad.Green, flags: Flags.ProDrums_Ghost),
            NewNote(23, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(24, DrumPad.Blue, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),
            NewNote(25, DrumPad.Orange, flags: Flags.ProDrums_Cymbal | Flags.ProDrums_Ghost),

            NewSpecial(26, SpecialPhrase.Type.Starpower, RESOLUTION * 8),
            NewNote(26, DrumPad.Red),
            NewNote(27, DrumPad.Yellow),
            NewNote(28, DrumPad.Blue),
            NewNote(29, DrumPad.Orange),
            NewNote(30, DrumPad.Green),
            NewNote(31, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(32, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(33, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(34, SpecialPhrase.Type.ProDrums_Activation, RESOLUTION * 5),
            NewNote(34, DrumPad.Red),
            NewNote(35, DrumPad.Yellow),
            NewNote(36, DrumPad.Blue),
            NewNote(37, DrumPad.Orange),
            NewNote(38, DrumPad.Green),
            NewNote(39, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(40, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(41, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(42, SpecialPhrase.Type.Versus_Player1, RESOLUTION * 8),
            NewNote(42, DrumPad.Red),
            NewNote(43, DrumPad.Yellow),
            NewNote(44, DrumPad.Blue),
            NewNote(45, DrumPad.Orange),
            NewNote(46, DrumPad.Green),
            NewNote(47, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(48, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(49, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(50, SpecialPhrase.Type.Versus_Player2, RESOLUTION * 8),
            NewNote(50, DrumPad.Red),
            NewNote(51, DrumPad.Yellow),
            NewNote(52, DrumPad.Blue),
            NewNote(53, DrumPad.Orange),
            NewNote(54, DrumPad.Green),
            NewNote(55, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(56, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(57, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(58, SpecialPhrase.Type.TremoloLane, RESOLUTION * 6),
            NewNote(58, DrumPad.Red),
            NewNote(59, DrumPad.Red),
            NewNote(60, DrumPad.Red),
            NewNote(61, DrumPad.Red),
            NewNote(62, DrumPad.Red),
            NewNote(63, DrumPad.Red),

            NewSpecial(64, SpecialPhrase.Type.TrillLane, RESOLUTION * 6),
            NewNote(64, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(65, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(66, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(67, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(68, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(69, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
        };

        public static MoonSong GenerateSong()
        {
            var song = new MoonSong();
            PopulateSyncTrack(song, TempoMap);
            PopulateGlobalEvents(song, GlobalEvents);
            foreach (var instrument in EnumX<MoonInstrument>.Values)
            {
                var gameMode = MoonSong.InstumentToChartGameMode(instrument);
                var data = GameModeToChartData(gameMode);
                PopulateInstrument(song, instrument, data);
            }
            return song;
        }

        public static List<ChartObject> GameModeToChartData(GameMode gameMode)
        {
            return gameMode switch {
                GameMode.Guitar => GuitarTrack,
                GameMode.GHLGuitar => GhlGuitarTrack,
                GameMode.ProGuitar => ProGuitarTrack,
                GameMode.Drums => DrumsTrack,
                GameMode.Vocals => new(), // TODO
                _ => throw new NotImplementedException($"No note data for game mode {gameMode}")
            };
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
            foreach (var chartObj in data)
            {
                chart.Add(chartObj.Clone(), false);
            }
            chart.UpdateCache();
        }

        public static void VerifySong(MoonSong sourceSong, MoonSong parsedSong, IEnumerable<GameMode> supportedModes)
        {
            Assert.Multiple(() =>
            {
                VerifyMetadata(sourceSong, parsedSong);
                VerifySync(sourceSong, parsedSong);
                foreach (var instrument in EnumX<MoonInstrument>.Values)
                {
                    // Skip unsupported instruments
                    var gameMode = MoonSong.InstumentToChartGameMode(instrument);
                    if (!supportedModes.Contains(gameMode))
                        continue;

                    VerifyInstrument(sourceSong, parsedSong, instrument);
                }
            });
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

        public static void VerifyInstrument(MoonSong sourceSong, MoonSong parsedSong, MoonInstrument instrument)
        {
            foreach (var difficulty in EnumX<Difficulty>.Values)
            {
                VerifyDifficulty(sourceSong, parsedSong, instrument, difficulty);
            }
        }

        public static void VerifyDifficulty(MoonSong sourceSong, MoonSong parsedSong, MoonInstrument instrument, Difficulty difficulty)
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