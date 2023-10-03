using System.Text;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Extensions;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static ChartIOHelper;
    using static TextEventDefinitions;
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

        private static readonly List<SpecialPhrase.Type> DrumsOnlySpecialPhrases = new()
        {
            SpecialPhrase.Type.Starpower,
            SpecialPhrase.Type.Versus_Player1,
            SpecialPhrase.Type.Versus_Player2,
            SpecialPhrase.Type.TremoloLane,
            SpecialPhrase.Type.TrillLane,
            SpecialPhrase.Type.ProDrums_Activation,
        };

        private const string NEWLINE = "\r\n";

        private static void GenerateSongSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_SONG}]{NEWLINE}{{{NEWLINE}");
            builder.Append($"  Resolution = {sourceSong.resolution}");
            builder.Append($"}}{NEWLINE}");
        }

        private static void GenerateSyncSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_SYNC_TRACK}]{NEWLINE}{{{NEWLINE}");
            int timeSigIndex = 0;
            int bpmIndex = 0;
            while (timeSigIndex < sourceSong.timeSignatures.Count ||
                   bpmIndex < sourceSong.bpms.Count)
            {
                // Generate in this order: phrases, notes, then events
                while (timeSigIndex < sourceSong.timeSignatures.Count &&
                    (bpmIndex == sourceSong.bpms.Count || sourceSong.timeSignatures[timeSigIndex].tick <= sourceSong.bpms[bpmIndex].tick))
                {
                    var ts = sourceSong.timeSignatures[timeSigIndex++];
                    builder.Append($"  {ts.tick} = TS {ts.numerator} {(int) Math.Log2(ts.denominator)}");
                }

                while (bpmIndex < sourceSong.bpms.Count &&
                    (timeSigIndex == sourceSong.timeSignatures.Count || sourceSong.bpms[bpmIndex].tick < sourceSong.timeSignatures[timeSigIndex].tick))
                {
                    var bpm = sourceSong.bpms[bpmIndex++];
                    builder.Append($"  {bpm.tick} = B {bpm.value}");
                }
            }
            builder.Append($"}}{NEWLINE}");
        }

        private static void GenerateEventsSection(MoonSong sourceSong, StringBuilder builder)
        {
            builder.Append($"[{SECTION_EVENTS}]{NEWLINE}{{{NEWLINE}");
            int sectionIndex = 0;
            int eventIndex = 0;
            while (sectionIndex < sourceSong.sections.Count ||
                   eventIndex < sourceSong.events.Count)
            {
                // Generate in this order: phrases, notes, then events
                while (sectionIndex < sourceSong.sections.Count &&
                    (eventIndex == sourceSong.events.Count || sourceSong.sections[sectionIndex].tick <= sourceSong.events[eventIndex].tick))
                {
                    var section = sourceSong.sections[sectionIndex++];
                    builder.Append($"  {section.tick} = E \"{section.title}\"");
                }

                while (eventIndex < sourceSong.events.Count &&
                    (sectionIndex == sourceSong.sections.Count || sourceSong.bpms[eventIndex].tick < sourceSong.sections[sectionIndex].tick))
                {
                    var ev = sourceSong.events[eventIndex++];
                    builder.Append($"  {ev.tick} = E \"{ev.title}\"");
                }
            }
            builder.Append($"}}{NEWLINE}");
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
            builder.Append($"[{difficultyName}{instrumentName}]{NEWLINE}{{{NEWLINE}");

            List<SpecialPhrase> phrasesToRemove = new();
            int noteIndex = 0;
            int phraseIndex = 0;
            int eventIndex = 0;
            while (noteIndex < chart.notes.Count ||
                   phraseIndex < chart.specialPhrases.Count ||
                   eventIndex < chart.events.Count)
            {
                // Generate in this order: phrases, notes, then events
                while (phraseIndex < chart.specialPhrases.Count &&
                    (noteIndex  == chart.notes.Count  || chart.specialPhrases[phraseIndex].tick <= chart.notes[noteIndex].tick) &&
                    (eventIndex == chart.events.Count || chart.specialPhrases[phraseIndex].tick <= chart.events[eventIndex].tick))
                {
                    var phrase = chart.specialPhrases[phraseIndex++];
                    // Drums-only phrases
                    if (DrumsOnlySpecialPhrases.Contains(phrase.type) && gameMode is not GameMode.Drums)
                    {
                        phrasesToRemove.Add(phrase);
                        continue;
                    }

                    // Solos are written as text events in .chart
                    if (phrase.type is SpecialPhrase.Type.Solo)
                    {
                        builder.Append($"  {phrase.tick} = E {SOLO_START}{NEWLINE}");
                        builder.Append($"  {phrase.tick + phrase.length} = E {SOLO_END}{NEWLINE}");
                        continue;
                    }

                    int phraseNumber = SpecialPhraseLookup[phrase.type];
                    builder.Append($"  {phrase.tick} = S {phraseNumber} {phrase.length}{NEWLINE}");
                }

                while (noteIndex < chart.notes.Count &&
                    (phraseIndex == chart.specialPhrases.Count || chart.notes[noteIndex].tick <  chart.specialPhrases[phraseIndex].tick) &&
                    (eventIndex  == chart.events.Count         || chart.notes[noteIndex].tick <= chart.events[eventIndex].tick))
                    AppendNote(builder, chart.notes[noteIndex++], gameMode);

                while (eventIndex < chart.events.Count &&
                    (phraseIndex == chart.specialPhrases.Count || chart.events[eventIndex].tick < chart.specialPhrases[phraseIndex].tick) &&
                    (noteIndex   == chart.notes.Count          || chart.events[eventIndex].tick < chart.notes[noteIndex].tick))
                {
                    var ev = chart.events[eventIndex++];
                    builder.Append($"  {ev.tick} = E {ev.eventName}{NEWLINE}");
                }
            }

            foreach (var phrase in phrasesToRemove)
            {
                chart.Remove(phrase);
            }

            builder.Append($"}}{NEWLINE}");
        }

        private static void AppendNote(StringBuilder builder, MoonNote note, GameMode gameMode)
        {
            uint tick = note.tick;
            var flags = note.flags;

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

            builder.Append($"  {tick} = N {chartNumber} {note.length}{NEWLINE}");
            if (canForce && (flags & Flags.Forced) != 0)
                builder.Append($"  {tick} = N 5 0{NEWLINE}");
            if (canTap && (flags & Flags.Tap) != 0)
                builder.Append($"  {tick} = N 6 0{NEWLINE}");
            if (canCymbal && (flags & Flags.ProDrums_Cymbal) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_PRO_DRUMS + chartNumber} 0{NEWLINE}");
            if (canDynamics && (flags & Flags.ProDrums_Accent) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_ACCENT + chartNumber} 0{NEWLINE}");
            if (canDynamics && (flags & Flags.ProDrums_Ghost) != 0)
                builder.Append($"  {tick} = N {NOTE_OFFSET_DRUMS_GHOST + chartNumber} 0{NEWLINE}");
        }

        private static string GenerateChartFile(MoonSong sourceSong)
        {
            var chartBuilder = new StringBuilder(5000);
            GenerateSongSection(sourceSong, chartBuilder);
            GenerateSyncSection(sourceSong, chartBuilder);
            GenerateEventsSection(sourceSong, chartBuilder);
            foreach (var instrument in EnumExtensions<MoonInstrument>.Values)
            {
                foreach (var difficulty in EnumExtensions<Difficulty>.Values)
                {
                    GenerateInstrumentSection(sourceSong, chartBuilder, instrument, difficulty);
                }
            }
            return chartBuilder.ToString();
        }

        public static string GenerateChartFile()
        {
            var song = GenerateSong();
            return GenerateChartFile(song);
        }

        [TestCase]
        public void GenerateAndParseChartFile()
        {
            YargTrace.AddListener(new YargDebugTraceListener());

            var sourceSong = GenerateSong();
            string chartText = GenerateChartFile(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = ChartReader.ReadFromText(Settings, chartText);
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