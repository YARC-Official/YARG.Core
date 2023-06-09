using System.Text;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using MoonscraperEngine;
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

        private static void GenerateSection(StringBuilder builder, List<MoonNote> data, MoonInstrument instrument, Difficulty difficulty)
        {
            string instrumentName = InstrumentToNameLookup[instrument];
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            string difficultyName = DifficultyToNameLookup[difficulty];
            builder.Append($"[{difficultyName}{instrumentName}]\n{{\n");

            bool canForce = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canCymbal = gameMode is GameMode.Drums;
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            var noteLookup = InstrumentToNoteLookupLookup[gameMode];
            for (int index = 0; index < data.Count; index++)
            {
                uint tick = RESOLUTION * (uint)index;
                var note = data[index];
                var flags = note.flags;

                // Not technically necessary, but might as well lol
                int rawNote = gameMode switch {
                    GameMode.Guitar => (int)note.guitarFret,
                    GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                    GameMode.Drums => (int)note.drumPad,
                    _ => note.rawNote
                };

                int chartNumber = noteLookup[rawNote];
                if (canDoubleKick && (flags & Flags.DoubleKick) != 0)
                    chartNumber = NOTE_OFFSET_INSTRUMENT_PLUS;

                builder.Append($"  {tick} = N {chartNumber} {note.length}\n");
                if (canForce && (flags & Flags.Forced) != 0)
                    builder.Append($"  {tick} = N 5 0\n");
                if (canTap && (flags & Flags.Tap) != 0)
                    builder.Append($"  {tick} = N 6 0\n");
                if (canCymbal && (flags & Flags.ProDrums_Cymbal) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_PRO_DRUMS + chartNumber} 0\n");
                if (canDynamics && (flags & Flags.ProDrums_Accent) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_ACCENT + chartNumber} 0\n");
                if (canDynamics && (flags & Flags.ProDrums_Ghost) != 0)
                    builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_GHOST + chartNumber} 0\n");
            }
            builder.Append("}\n");
        }

        private static string GenerateChartFile()
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

            var chartBuilder = new StringBuilder(header, 1000);
            foreach (var difficulty in EnumX<Difficulty>.Values)
            {
                GenerateSection(chartBuilder, GuitarNotes, MoonInstrument.Guitar, difficulty);
                GenerateSection(chartBuilder, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, difficulty);
                GenerateSection(chartBuilder, DrumsNotes, MoonInstrument.Drums, difficulty);
            }
            return chartBuilder.ToString();
        }

        [TestCase]
        public void GenerateAndParseChartFile()
        {
            string chartText = GenerateChartFile();
            MoonSong song;
            try
            {
                song = ChartReader.ReadChart(new StringReader(chartText));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            Assert.Multiple(() =>
            {
                VerifyMetadata(song);
                VerifySync(song);
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    VerifyTrack(song, GuitarNotes, MoonInstrument.Guitar, difficulty);
                    VerifyTrack(song, GhlGuitarNotes, MoonInstrument.GHLiveGuitar, difficulty);
                    VerifyTrack(song, DrumsNotes, MoonInstrument.Drums, difficulty);
                }
            });
        }
    }
}