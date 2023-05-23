using System.Text;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static ChartIOHelper;
    using static ParseBehaviorTests;

    public class ChartParseBehaviorTests
    {
        private static readonly Dictionary<MoonInstrument, string> InstrumentToNameLookup =
            InstrumentStrToEnumLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<Difficulty, string> DifficultyToNameLookup =
            TrackNameToTrackDifficultyLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<int, int> GuitarNoteLookup = new()
        {
            { (int)GuitarFret.Green,  0 },
            { (int)GuitarFret.Red,    1 },
            { (int)GuitarFret.Yellow, 2 },
            { (int)GuitarFret.Blue,   3 },
            { (int)GuitarFret.Orange, 4 },
            { (int)GuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, int> GhlGuitarNoteLookup = new()
        {
            { (int)GHLiveGuitarFret.Black1, 3 },
            { (int)GHLiveGuitarFret.Black2, 4 },
            { (int)GHLiveGuitarFret.Black3, 8 },
            { (int)GHLiveGuitarFret.White1, 0 },
            { (int)GHLiveGuitarFret.White2, 1 },
            { (int)GHLiveGuitarFret.White3, 2 },
            { (int)GHLiveGuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, int> DrumsNoteLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, int>> InstrumentToNoteLookupLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteLookup },
            { GameMode.Drums,     DrumsNoteLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteLookup },
        };

        private static void GenerateSection(StringBuilder builder, List<NoteData> data, MoonInstrument instrument, Difficulty difficulty)
        {
            string instrumentName = InstrumentToNameLookup[instrument];
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            string difficultyName = DifficultyToNameLookup[difficulty];
            builder.Append($"[{difficultyName}{instrumentName}]\n{{\n");

            var noteLookup = InstrumentToNoteLookupLookup[gameMode];
            for (int index = 0; index < data.Count; index++)
            {
                uint tick = RESOLUTION * (uint)index;
                var note = data[index];
                var flags = note.flags;

                int chartNumber = noteLookup[note.number];
                if ((flags & Flags.DoubleKick) != 0)
                    chartNumber = NOTE_OFFSET_INSTRUMENT_PLUS;

                builder.Append($"  {tick} = N {chartNumber} {note.length}\n");
                if (gameMode != GameMode.Drums && (flags & Flags.Forced) != 0)
                    builder.Append($"  {tick} = N 5 0\n");
                if (gameMode != GameMode.Drums && (flags & Flags.Tap) != 0)
                    builder.Append($"  {tick} = N 6 0\n");
                if (gameMode == GameMode.Drums && (flags & Flags.ProDrums_Cymbal) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_PRO_DRUMS + chartNumber} 0\n");
                if (gameMode == GameMode.Drums && (flags & Flags.ProDrums_Accent) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_ACCENT + chartNumber} 0\n");
                if (gameMode == GameMode.Drums && (flags & Flags.ProDrums_Ghost) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_GHOST + chartNumber} 0\n");
            }
            builder.Append("}\n");
        }

        [TestCase]
        public void GenerateAndParseChartFile()
        {
            string header = $$"""
                {{SECTION_SONG}}
                {
                  Resolution = {{RESOLUTION}}
                }
                {{SECTION_SYNC_TRACK}}
                {
                  {{RESOLUTION * 0}} = TS {{NUMERATOR}} {{DENOMINATOR_POW2}}
                  {{RESOLUTION * 0}} = B {{(int)(TEMPO * 1000)}}
                }

                """; // Trailing newline is deliberate

            var chartText = new StringBuilder(header, 1000);
            GenerateSection(chartText, GuitarNotes, MoonInstrument.Guitar, Difficulty.Expert);
            GenerateSection(chartText, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, Difficulty.Expert);
            GenerateSection(chartText, DrumsNotes, MoonInstrument.Drums, Difficulty.Expert);

            MoonSong song;
            try
            {
                song = ChartReader.ReadChart(new StringReader(chartText.ToString()));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            Assert.Multiple(() =>
            {
                VerifyTrack(song, GuitarNotes, MoonInstrument.Guitar, Difficulty.Expert);
                VerifyTrack(song, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, Difficulty.Expert);
                VerifyTrack(song, DrumsNotes, MoonInstrument.Drums, Difficulty.Expert);
            });
        }
    }
}