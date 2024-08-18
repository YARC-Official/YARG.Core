using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;

    internal class ParseBehaviorTests
    {
        public const uint RESOLUTION = 192;
        public const float TEMPO = 120;
        public const int NUMERATOR = 4;
        public const int DENOMINATOR = 4;

        public static readonly List<MoonText> GlobalEvents = new()
        {
        };

        private static MoonNote NewNote(int index, int rawNote, float length = 0, Flags flags = Flags.None)
            => new((uint) (index * RESOLUTION), rawNote, (uint) (length * RESOLUTION), flags);
        private static MoonNote NewNote(int index, GuitarFret fret, float length = 0, Flags flags = Flags.None)
            => NewNote(index, (int) fret, length, flags);
        private static MoonNote NewNote(int index, GHLiveGuitarFret fret, float length = 0, Flags flags = Flags.None)
            => NewNote(index, (int) fret, length, flags);
        private static MoonNote NewNote(int index, DrumPad pad, float length = 0, Flags flags = Flags.None)
            => NewNote(index, (int) pad, length, flags);
        private static MoonNote NewNote(int index, ProGuitarString str, int fret, float length = 0, Flags flags = Flags.None)
            => NewNote(index, MoonNote.MakeProGuitarRawNote(str, fret), length, flags);
        private static MoonPhrase NewSpecial(int index, MoonPhrase.Type type, float length = 0)
            => new((uint) (index * RESOLUTION), (uint) (length * RESOLUTION), type);

        public static readonly MoonChart GuitarTrack = new(GameMode.Guitar)
        {
            NewNote(0, GuitarFret.Green),
            NewNote(1, GuitarFret.Red),
            NewNote(2, GuitarFret.Yellow),
            NewNote(3, GuitarFret.Blue),
            NewNote(4, GuitarFret.Orange),
            NewNote(5, GuitarFret.Open),

            NewSpecial(6, MoonPhrase.Type.Versus_Player1, length: 6),
            NewNote(6, GuitarFret.Green, flags: Flags.Forced),
            NewNote(7, GuitarFret.Red, flags: Flags.Forced),
            NewNote(8, GuitarFret.Yellow, flags: Flags.Forced),
            NewNote(9, GuitarFret.Blue, flags: Flags.Forced),
            NewNote(10, GuitarFret.Orange, flags: Flags.Forced),
            NewNote(11, GuitarFret.Open, flags: Flags.Forced),

            NewSpecial(12, MoonPhrase.Type.Versus_Player2, length: 5),
            NewNote(12, GuitarFret.Green, flags: Flags.Tap),
            NewNote(13, GuitarFret.Red, flags: Flags.Tap),
            NewNote(14, GuitarFret.Yellow, flags: Flags.Tap),
            NewNote(15, GuitarFret.Blue, flags: Flags.Tap),
            NewNote(16, GuitarFret.Orange, flags: Flags.Tap),

            NewSpecial(17, MoonPhrase.Type.Starpower, length: 6),
            NewNote(17, GuitarFret.Green),
            NewNote(18, GuitarFret.Red),
            NewNote(19, GuitarFret.Yellow),
            NewNote(20, GuitarFret.Blue),
            NewNote(21, GuitarFret.Orange),
            NewNote(22, GuitarFret.Open),

            NewSpecial(23, MoonPhrase.Type.TremoloLane, length: 6),
            NewNote(23, GuitarFret.Yellow),
            NewNote(24, GuitarFret.Yellow),
            NewNote(25, GuitarFret.Yellow),
            NewNote(26, GuitarFret.Yellow),
            NewNote(27, GuitarFret.Yellow),
            NewNote(28, GuitarFret.Yellow),

            NewSpecial(29, MoonPhrase.Type.TrillLane, length: 6),
            NewNote(29, GuitarFret.Green),
            NewNote(30, GuitarFret.Red),
            NewNote(31, GuitarFret.Green),
            NewNote(32, GuitarFret.Red),
            NewNote(33, GuitarFret.Green),
            NewNote(34, GuitarFret.Red),

            NewSpecial(35, MoonPhrase.Type.Solo, length: 6),
            NewNote(35, GuitarFret.Green, flags: Flags.Forced),
            NewNote(36, GuitarFret.Red, flags: Flags.Forced),
            NewNote(37, GuitarFret.Yellow, flags: Flags.Forced),
            NewNote(38, GuitarFret.Blue, flags: Flags.Forced),
            NewNote(39, GuitarFret.Orange, flags: Flags.Forced),
            NewNote(40, GuitarFret.Open, flags: Flags.Forced),

            NewNote(41, GuitarFret.Green, length: 1),
            NewNote(42, GuitarFret.Red, length: 1),
            NewNote(43, GuitarFret.Yellow, length: 1),
            NewNote(44, GuitarFret.Blue, length: 1),
            NewNote(45, GuitarFret.Orange, length: 1),
            NewNote(46, GuitarFret.Open, length: 1),
        };

        public static readonly MoonChart GhlGuitarTrack = new(GameMode.GHLGuitar)
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

            NewSpecial(20, MoonPhrase.Type.Starpower, length: 7),
            NewNote(20, GHLiveGuitarFret.Black1),
            NewNote(21, GHLiveGuitarFret.Black2),
            NewNote(22, GHLiveGuitarFret.Black3),
            NewNote(23, GHLiveGuitarFret.White1),
            NewNote(24, GHLiveGuitarFret.White2),
            NewNote(25, GHLiveGuitarFret.White3),
            NewNote(26, GHLiveGuitarFret.Open),

            NewSpecial(27, MoonPhrase.Type.Solo, length: 7),
            NewNote(27, GHLiveGuitarFret.Black1),
            NewNote(28, GHLiveGuitarFret.Black2),
            NewNote(29, GHLiveGuitarFret.Black3),
            NewNote(30, GHLiveGuitarFret.White1),
            NewNote(31, GHLiveGuitarFret.White2),
            NewNote(32, GHLiveGuitarFret.White3),
            NewNote(33, GHLiveGuitarFret.Open),
        };

        public static readonly MoonChart ProGuitarTrack = new(GameMode.ProGuitar)
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

            NewSpecial(18, MoonPhrase.Type.Starpower, length: 6),
            NewNote(18, ProGuitarString.Red, 0),
            NewNote(19, ProGuitarString.Green, 1),
            NewNote(20, ProGuitarString.Orange, 2),
            NewNote(21, ProGuitarString.Blue, 3),
            NewNote(22, ProGuitarString.Yellow, 4),
            NewNote(23, ProGuitarString.Purple, 5),

            NewSpecial(24, MoonPhrase.Type.TremoloLane, length: 6),
            NewNote(24, ProGuitarString.Red, 0),
            NewNote(25, ProGuitarString.Red, 0),
            NewNote(26, ProGuitarString.Red, 0),
            NewNote(27, ProGuitarString.Red, 0),
            NewNote(28, ProGuitarString.Red, 0),
            NewNote(29, ProGuitarString.Red, 0),

            NewSpecial(30, MoonPhrase.Type.TrillLane, length: 6),
            NewNote(30, ProGuitarString.Yellow, 5),
            NewNote(31, ProGuitarString.Yellow, 6),
            NewNote(32, ProGuitarString.Yellow, 5),
            NewNote(33, ProGuitarString.Yellow, 6),
            NewNote(34, ProGuitarString.Yellow, 5),
            NewNote(35, ProGuitarString.Yellow, 6),

            NewSpecial(36, MoonPhrase.Type.Solo, length: 6),
            NewNote(36, ProGuitarString.Red, 0),
            NewNote(37, ProGuitarString.Green, 1),
            NewNote(38, ProGuitarString.Orange, 2),
            NewNote(39, ProGuitarString.Blue, 3),
            NewNote(40, ProGuitarString.Yellow, 4),
            NewNote(41, ProGuitarString.Purple, 5),
        };

        public static readonly MoonChart DrumsTrack = new(GameMode.Drums)
        {
            NewNote(0, DrumPad.Kick),
            NewNote(1, DrumPad.Kick, flags: Flags.DoubleKick),

            NewNote(2, DrumPad.Red, length: 1),
            NewNote(3, DrumPad.Yellow, length: 1),
            NewNote(4, DrumPad.Blue, length: 1),
            NewNote(5, DrumPad.Orange, length: 1),
            NewNote(6, DrumPad.Green, length: 1),
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

            NewSpecial(26, MoonPhrase.Type.Starpower, length: 8),
            NewNote(26, DrumPad.Red),
            NewNote(27, DrumPad.Yellow),
            NewNote(28, DrumPad.Blue),
            NewNote(29, DrumPad.Orange),
            NewNote(30, DrumPad.Green),
            NewNote(31, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(32, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(33, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(34, MoonPhrase.Type.ProDrums_Activation, length: 5),
            NewNote(34, DrumPad.Red),
            NewNote(35, DrumPad.Yellow),
            NewNote(36, DrumPad.Blue),
            NewNote(37, DrumPad.Orange),
            NewNote(38, DrumPad.Green),
            NewNote(39, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(40, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(41, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(42, MoonPhrase.Type.Versus_Player1, length: 8),
            NewNote(42, DrumPad.Red),
            NewNote(43, DrumPad.Yellow),
            NewNote(44, DrumPad.Blue),
            NewNote(45, DrumPad.Orange),
            NewNote(46, DrumPad.Green),
            NewNote(47, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(48, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(49, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(50, MoonPhrase.Type.Versus_Player2, length: 8),
            NewNote(50, DrumPad.Red),
            NewNote(51, DrumPad.Yellow),
            NewNote(52, DrumPad.Blue),
            NewNote(53, DrumPad.Orange),
            NewNote(54, DrumPad.Green),
            NewNote(55, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(56, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(57, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(58, MoonPhrase.Type.TremoloLane, length: 6),
            NewNote(58, DrumPad.Red),
            NewNote(59, DrumPad.Red),
            NewNote(60, DrumPad.Red),
            NewNote(61, DrumPad.Red),
            NewNote(62, DrumPad.Red),
            NewNote(63, DrumPad.Red),

            NewSpecial(64, MoonPhrase.Type.TrillLane, length: 6),
            NewNote(64, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(65, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(66, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(67, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
            NewNote(68, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(69, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),

            NewSpecial(70, MoonPhrase.Type.Solo, length: 8),
            NewNote(70, DrumPad.Red),
            NewNote(71, DrumPad.Yellow),
            NewNote(72, DrumPad.Blue),
            NewNote(73, DrumPad.Orange),
            NewNote(74, DrumPad.Green),
            NewNote(75, DrumPad.Yellow, flags: Flags.ProDrums_Cymbal),
            NewNote(76, DrumPad.Blue, flags: Flags.ProDrums_Cymbal),
            NewNote(77, DrumPad.Orange, flags: Flags.ProDrums_Cymbal),
        };

        private const byte VOCALS_RANGE_START = MidIOHelper.VOCALS_RANGE_START;

        public static readonly MoonChart VocalsNotes = new(GameMode.Vocals)
        {
            NewSpecial(0, MoonPhrase.Type.Versus_Player1, length: 12),
            NewSpecial(0, MoonPhrase.Type.Vocals_LyricPhrase, length: 12),
            NewNote(0, VOCALS_RANGE_START + 0, length: 0.5f),
            NewNote(1, VOCALS_RANGE_START + 1, length: 0.5f),
            NewNote(2, VOCALS_RANGE_START + 2, length: 0.5f),
            NewNote(3, VOCALS_RANGE_START + 3, length: 0.5f),
            NewNote(4, VOCALS_RANGE_START + 4, length: 0.5f),
            NewNote(5, VOCALS_RANGE_START + 5, length: 0.5f),
            NewNote(6, VOCALS_RANGE_START + 6, length: 0.5f),
            NewNote(7, VOCALS_RANGE_START + 7, length: 0.5f),
            NewNote(8, VOCALS_RANGE_START + 8, length: 0.5f),
            NewNote(9, VOCALS_RANGE_START + 9, length: 0.5f),
            NewNote(10, VOCALS_RANGE_START + 10, length: 0.5f),
            NewNote(11, VOCALS_RANGE_START + 11, length: 0.5f),

            NewSpecial(12, MoonPhrase.Type.Versus_Player2, length: 12),
            NewSpecial(12, MoonPhrase.Type.Vocals_LyricPhrase, length: 12),
            NewSpecial(12, MoonPhrase.Type.Starpower, length: 12),
            NewNote(12, VOCALS_RANGE_START + 12, length: 0.5f),
            NewNote(13, VOCALS_RANGE_START + 13, length: 0.5f),
            NewNote(14, VOCALS_RANGE_START + 14, length: 0.5f),
            NewNote(15, VOCALS_RANGE_START + 15, length: 0.5f),
            NewNote(16, VOCALS_RANGE_START + 16, length: 0.5f),
            NewNote(17, VOCALS_RANGE_START + 17, length: 0.5f),
            NewNote(18, VOCALS_RANGE_START + 18, length: 0.5f),
            NewNote(19, VOCALS_RANGE_START + 19, length: 0.5f),
            NewNote(20, VOCALS_RANGE_START + 20, length: 0.5f),
            NewNote(21, VOCALS_RANGE_START + 21, length: 0.5f),
            NewNote(22, VOCALS_RANGE_START + 22, length: 0.5f),
            NewNote(23, VOCALS_RANGE_START + 23, length: 0.5f),

            NewSpecial(24, MoonPhrase.Type.Versus_Player1, length: 12),
            NewSpecial(24, MoonPhrase.Type.Versus_Player2, length: 12),
            NewSpecial(24, MoonPhrase.Type.Vocals_LyricPhrase, length: 12),
            NewNote(24, VOCALS_RANGE_START + 24, length: 0.5f),
            NewNote(25, VOCALS_RANGE_START + 25, length: 0.5f),
            NewNote(26, VOCALS_RANGE_START + 26, length: 0.5f),
            NewNote(27, VOCALS_RANGE_START + 27, length: 0.5f),
            NewNote(28, VOCALS_RANGE_START + 28, length: 0.5f),
            NewNote(29, VOCALS_RANGE_START + 29, length: 0.5f),
            NewNote(30, VOCALS_RANGE_START + 30, length: 0.5f),
            NewNote(31, VOCALS_RANGE_START + 31, length: 0.5f),
            NewNote(32, VOCALS_RANGE_START + 32, length: 0.5f),
            NewNote(33, VOCALS_RANGE_START + 33, length: 0.5f),
            NewNote(34, VOCALS_RANGE_START + 34, length: 0.5f),
            NewNote(35, VOCALS_RANGE_START + 35, length: 0.5f),

            NewSpecial(36, MoonPhrase.Type.Versus_Player2, length: 13),
            NewSpecial(36, MoonPhrase.Type.Vocals_LyricPhrase, length: 13),
            NewNote(36, VOCALS_RANGE_START + 36, length: 0.5f),
            NewNote(37, VOCALS_RANGE_START + 37, length: 0.5f),
            NewNote(38, VOCALS_RANGE_START + 38, length: 0.5f),
            NewNote(39, VOCALS_RANGE_START + 39, length: 0.5f),
            NewNote(40, VOCALS_RANGE_START + 40, length: 0.5f),
            NewNote(41, VOCALS_RANGE_START + 41, length: 0.5f),
            NewNote(42, VOCALS_RANGE_START + 42, length: 0.5f),
            NewNote(43, VOCALS_RANGE_START + 43, length: 0.5f),
            NewNote(44, VOCALS_RANGE_START + 44, length: 0.5f),
            NewNote(45, VOCALS_RANGE_START + 45, length: 0.5f),
            NewNote(46, VOCALS_RANGE_START + 46, length: 0.5f),
            NewNote(47, VOCALS_RANGE_START + 47, length: 0.5f),
            NewNote(48, VOCALS_RANGE_START + 48, length: 0.5f),

            NewSpecial(49, MoonPhrase.Type.Versus_Player1, length: 1),
            NewSpecial(49, MoonPhrase.Type.Vocals_LyricPhrase, length: 1),
            NewNote(49, 0, flags: Flags.Vocals_Percussion),
        };

        public static readonly MoonChart ProKeysNotes = new(GameMode.ProKeys)
        {
            NewSpecial(0, MoonPhrase.Type.ProKeys_RangeShift0),
            NewNote(0, 0, length: 1),
            NewNote(1, 1, length: 1),
            NewNote(2, 2, length: 1),
            NewNote(3, 3, length: 1),

            NewSpecial(4, MoonPhrase.Type.ProKeys_RangeShift1),
            NewNote(4, 4, length: 1),
            NewNote(5, 5, length: 1),
            NewNote(6, 6, length: 1),
            NewNote(7, 7, length: 1),

            NewSpecial(8, MoonPhrase.Type.Starpower, length: 8),
            NewSpecial(8, MoonPhrase.Type.ProKeys_RangeShift2),
            NewNote(8, 8, length: 1),
            NewNote(9, 9, length: 1),
            NewNote(10, 10, length: 1),
            NewNote(11, 11, length: 1),

            NewSpecial(12, MoonPhrase.Type.ProKeys_RangeShift3),
            NewNote(12, 12, length: 1),
            NewNote(13, 13, length: 1),
            NewNote(14, 14, length: 1),
            NewNote(15, 15, length: 1),

            NewSpecial(16, MoonPhrase.Type.Solo, length: 9),
            NewSpecial(16, MoonPhrase.Type.ProKeys_RangeShift4),
            NewNote(16, 16, length: 1),
            NewNote(17, 17, length: 1),
            NewNote(18, 18, length: 1),
            NewNote(19, 19, length: 1),

            NewSpecial(20, MoonPhrase.Type.ProKeys_RangeShift5),
            NewNote(20, 20, length: 1),
            NewNote(21, 21, length: 1),
            NewNote(22, 22, length: 1),
            NewNote(23, 23, length: 1),
            NewNote(24, 24, length: 1),

            NewSpecial(25, MoonPhrase.Type.ProKeys_Glissando, length: 10),
            NewNote(25, 0),
            NewNote(26, 1),
            NewNote(27, 2),
            NewNote(28, 3),
            NewNote(29, 4),
            NewNote(30, 5),
            NewNote(31, 6),
            NewNote(32, 7),
            NewNote(33, 8),
            NewNote(34, 9),

            NewSpecial(35, MoonPhrase.Type.ProKeys_Glissando, length: 10),
            NewNote(35, 10),
            NewNote(36, 11),
            NewNote(37, 10),
            NewNote(38, 11),
            NewNote(39, 10),
            NewNote(40, 11),
            NewNote(41, 10),
            NewNote(42, 11),
            NewNote(43, 10),
            NewNote(44, 11),
        };

        public static MoonSong GenerateSong()
        {
            var song = new MoonSong(RESOLUTION);
            PopulateSyncTrack(song);
            PopulateGlobalEvents(song, GlobalEvents);
            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                var gameMode = InstrumentToChartGameMode(instrument);
                var track = GameModeToChartData(gameMode);
                PopulateInstrument(song, instrument, track);
            }
            return song;
        }

        public static MoonChart GameModeToChartData(GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.Guitar => GuitarTrack,
                GameMode.GHLGuitar => GhlGuitarTrack,
                GameMode.ProGuitar => ProGuitarTrack,
                GameMode.Drums => DrumsTrack,
                GameMode.Vocals => VocalsNotes,
                GameMode.ProKeys => ProKeysNotes,
                _ => throw new NotImplementedException($"No note data for game mode {gameMode}")
            };
        }

        public static void PopulateSyncTrack(MoonSong song)
        {
            song.Add(new TempoChange(TEMPO, 0, 0));
            song.Add(new TimeSignatureChange(NUMERATOR, DENOMINATOR, 0, 0));
        }

        public static void PopulateGlobalEvents(MoonSong song, List<MoonText> events)
        {
            foreach (var text in events)
            {
                song.Add(text.Clone());
            }
        }

        public static void PopulateInstrument(MoonSong song, MoonInstrument instrument, MoonChart track)
        {
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                PopulateDifficulty(song, instrument, difficulty, track);
            }
        }

        public static void PopulateDifficulty(MoonSong song, MoonInstrument instrument, Difficulty difficulty, MoonChart track)
        {
            var chart = song.GetChart(instrument, difficulty);
            foreach (var note in track.notes)
            {
                chart.Add(note.Clone());
            }

            foreach (var phrase in track.specialPhrases)
            {
                chart.Add(phrase.Clone());
            }
        }

        public static void VerifySong(MoonSong sourceSong, MoonSong parsedSong, IEnumerable<GameMode> supportedModes)
        {
            sourceSong.Sort();
            parsedSong.Sort();

            VerifyGlobal(sourceSong, parsedSong);

            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                // Skip unsupported instruments
                var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
                if (!supportedModes.Contains(gameMode))
                    continue;

                VerifyInstrument(sourceSong, parsedSong, instrument);
            }
        }

        public static void VerifyGlobal(MoonSong sourceSong, MoonSong parsedSong)
        {
            Assert.Multiple(() =>
            {
                Assert.That(parsedSong.resolution, Is.EqualTo(sourceSong.resolution), "Resolution was not parsed correctly!");
                CollectionAssert.AreEqual(parsedSong.events, parsedSong.events, "Global events do not match!");

                CollectionAssert.AreEqual(sourceSong.syncTrack.Tempos, parsedSong.syncTrack.Tempos, "BPMs do not match!");
                CollectionAssert.AreEqual(sourceSong.syncTrack.TimeSignatures, parsedSong.syncTrack.TimeSignatures, "Time signatures do not match!");
                CollectionAssert.AreEqual(sourceSong.syncTrack.Beatlines, parsedSong.syncTrack.Beatlines, "Beatlines do not match!");
            });
        }

        public static void VerifyInstrument(MoonSong sourceSong, MoonSong parsedSong, MoonInstrument instrument)
        {
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
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
                CollectionAssert.AreEqual(sourceChart.notes, parsedChart.notes, $"Notes on {difficulty} {instrument} do not match!");
                CollectionAssert.AreEqual(sourceChart.specialPhrases, parsedChart.specialPhrases, $"Special phrases on {difficulty} {instrument} do not match!");
                CollectionAssert.AreEqual(sourceChart.events, parsedChart.events, $"Local events on {difficulty} {instrument} do not match!");
            });
        }
    }
}