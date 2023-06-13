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

        private static readonly Dictionary<SpecialPhrase.Type, int> SpecialPhraseLookup = new()
        {
            { SpecialPhrase.Type.Starpower,           PHRASE_STARPOWER },
            { SpecialPhrase.Type.Versus_Player1,      PHRASE_VERSUS_PLAYER_1 },
            { SpecialPhrase.Type.Versus_Player2,      PHRASE_VERSUS_PLAYER_2 },
            { SpecialPhrase.Type.TremoloLane,         PHRASE_TREMOLO_LANE },
            { SpecialPhrase.Type.TrillLane,           PHRASE_TRILL_LANE },
            { SpecialPhrase.Type.ProDrums_Activation, PHRASE_DRUM_FILL },
        };

        private static void GenerateSongSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"{SECTION_SONG}\n{{\n");
            builder.Append($"  Resolution = {sourceSong.resolution}");
            builder.Append("}\n");
        }

        private static void GenerateSyncSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"{SECTION_SYNC_TRACK}\n{{\n");
            foreach (var sync in sourceSong.syncTrack)
            {
                switch (sync)
                {
                    case BPM bpm:
                        builder.Append($"  {bpm.tick} = B {bpm.value}");
                        break;
                    case TimeSignature ts:
                        builder.Append($"  {ts.tick} = TS {ts.numerator} {(int)Math.Log2(ts.denominator)}");
                        break;
                }
            }
            builder.Append("}\n");
        }

        private static void GenerateEventsSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"{SECTION_EVENTS}\n{{\n");
            foreach (var text in sourceSong.eventsAndSections)
            {
                builder.Append($"  {text.tick} = E \"{text.title}\"");
            }
            builder.Append("}\n");
        }

        private static void GenerateInstrumentSection(MoonSong sourceSong, StringBuilder builder, MoonInstrument instrument, Difficulty difficulty)
        {
            // Skip unsupported instruments
            var gameMode = MoonSong.InstumentToChartGameMode(instrument);
            if (!InstrumentToNoteLookupLookup.ContainsKey(gameMode))
                return;

            var chart = sourceSong.GetChart(instrument, difficulty);

            string instrumentName = InstrumentToNameLookup[instrument];
            string difficultyName = DifficultyToNameLookup[difficulty];
            builder.Append($"[{difficultyName}{instrumentName}]\n{{\n");

            List<ChartObject> eventsToRemove = new();
            foreach (var chartObj in chart.chartObjects)
            {
                switch (chartObj)
                {
                    case MoonNote note:
                        AppendNote(builder, note);
                        break;
                    case SpecialPhrase phrase:
                        // Drums-only phrases
                        if (gameMode is not GameMode.Drums && phrase.type is SpecialPhrase.Type.TremoloLane or
                            SpecialPhrase.Type.TrillLane or SpecialPhrase.Type.ProDrums_Activation)
                        {
                            eventsToRemove.Add(chartObj);
                            continue;
                        }
                        int phraseNumber = SpecialPhraseLookup[phrase.type];
                        builder.Append($"  {phrase.tick} = S {phraseNumber} {phrase.length}\n");
                        break;
                    case ChartEvent text:
                        builder.Append($"  {text.tick} = E {text.eventName}\n");
                        break;
                }
            }

            foreach (var chartObj in eventsToRemove)
            {
                chart.Remove(chartObj);
            }

            builder.Append("}\n");
        }

        private static void AppendNote(StringBuilder builder, MoonNote note)
        {
            uint tick = note.tick;
            var flags = note.flags;
            var gameMode = note.gameMode;

            bool canForce = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canTap = gameMode is GameMode.Guitar or GameMode.GHLGuitar;
            bool canCymbal = gameMode is GameMode.Drums;
            bool canDoubleKick = gameMode is GameMode.Drums;
            bool canDynamics = gameMode is GameMode.Drums;

            var noteLookup = InstrumentToNoteLookupLookup[gameMode];

            // Not technically necessary, but might as well lol
            int rawNote = gameMode switch {
                GameMode.Guitar => (int)note.guitarFret,
                GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                GameMode.ProGuitar => throw new NotSupportedException(".chart does not support Pro Guitar!"),
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

        private static string GenerateChartFile(MoonSong sourceSong)
        {
            var chartBuilder = new StringBuilder(5000);
            GenerateSongSection(sourceSong, chartBuilder);
            GenerateSyncSection(sourceSong, chartBuilder);
            GenerateEventsSection(sourceSong, chartBuilder);
            foreach (var instrument in EnumX<MoonInstrument>.Values)
            {
                foreach (var difficulty in EnumX<Difficulty>.Values)
                {
                    GenerateInstrumentSection(sourceSong, chartBuilder, instrument, difficulty);
                }
            }
            return chartBuilder.ToString();
        }

        [TestCase]
        public void GenerateAndParseChartFile()
        {
            var sourceSong = GenerateSong();
            string chartText = GenerateChartFile(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = ChartReader.ReadChart(new StringReader(chartText));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Chart parsing threw an exception!\n{ex}");
                return;
            }

            VerifySong(sourceSong, parsedSong, InstrumentToNoteLookupLookup.Keys);
        }
    }
}