using System.Text;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using NUnit.Framework;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.UnitTests.Parsing
{
    using static MoonSong;
    using static MoonChart;
    using static MoonNote;
    using static ChartIOHelper;
    using static TextEvents;
    using static ParseBehaviorTests;

    public class ChartParseBehaviorTests
    {
        private class ChartSection : IDisposable
        {
            private StringBuilder _builder;
            private List<(uint tick, string typeCode, string data)> _events = new();

            public ChartSection(StringBuilder builder, string sectionName)
            {
                _builder = builder;

                _builder.Append($"[{sectionName}]{NEWLINE}");
                _builder.Append($"{{{NEWLINE}");
            }

            public void AddEvent(uint tick, string typeCode, string data)
            {
                _events.Add((tick, typeCode, data));
            }

            public void AddEvent(uint tick, string typeCode, uint value1)
            {
                _events.Add((tick, typeCode, $"{value1}"));
            }

            public void AddEvent(uint tick, string typeCode, uint value1, uint value2)
            {
                _events.Add((tick, typeCode, $"{value1} {value2}"));
            }

            public void Dispose()
            {
                _events.Sort((left, right) =>
                {
                    int compare = left.tick.CompareTo(right.tick);
                    if (compare != 0)
                        return compare;

                    compare = string.Compare(left.typeCode, right.typeCode, StringComparison.Ordinal);
                    if (compare != 0)
                        return compare;

                    return string.Compare(left.data, right.data, StringComparison.Ordinal);
                });

                foreach (var (tick, typeCode, data) in _events)
                {
                    _builder.Append($"  {tick} = {typeCode} {data}{NEWLINE}");
                }

                _builder.Append($"}}{NEWLINE}");
            }
        }

        private static readonly Dictionary<MoonInstrument, string> InstrumentToNameLookup =
            InstrumentStrToEnumLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<Difficulty, string> DifficultyToNameLookup =
            TrackNameToTrackDifficultyLookup.ToDictionary((pair) => pair.Value, (pair) => pair.Key);

        private static readonly Dictionary<int, uint> GuitarNoteLookup = new()
        {
            { (int)GuitarFret.Green,  0 },
            { (int)GuitarFret.Red,    1 },
            { (int)GuitarFret.Yellow, 2 },
            { (int)GuitarFret.Blue,   3 },
            { (int)GuitarFret.Orange, 4 },
            { (int)GuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, uint> GhlGuitarNoteLookup = new()
        {
            { (int)GHLiveGuitarFret.Black1, 3 },
            { (int)GHLiveGuitarFret.Black2, 4 },
            { (int)GHLiveGuitarFret.Black3, 8 },
            { (int)GHLiveGuitarFret.White1, 0 },
            { (int)GHLiveGuitarFret.White2, 1 },
            { (int)GHLiveGuitarFret.White3, 2 },
            { (int)GHLiveGuitarFret.Open,   7 },
        };

        private static readonly Dictionary<int, uint> DrumsNoteLookup = new()
        {
            { (int)DrumPad.Kick,   0 },
            { (int)DrumPad.Red,    1 },
            { (int)DrumPad.Yellow, 2 },
            { (int)DrumPad.Blue,   3 },
            { (int)DrumPad.Orange, 4 },
            { (int)DrumPad.Green,  5 },
        };

        private static readonly Dictionary<GameMode, Dictionary<int, uint>> InstrumentToNoteLookupLookup = new()
        {
            { GameMode.Guitar,    GuitarNoteLookup },
            { GameMode.Drums,     DrumsNoteLookup },
            { GameMode.GHLGuitar, GhlGuitarNoteLookup },
        };

        private static readonly Dictionary<MoonPhrase.Type, uint> SpecialPhraseLookup = new()
        {
            { MoonPhrase.Type.Starpower,           PHRASE_STARPOWER },
            { MoonPhrase.Type.Versus_Player1,      PHRASE_VERSUS_PLAYER_1 },
            { MoonPhrase.Type.Versus_Player2,      PHRASE_VERSUS_PLAYER_2 },
            { MoonPhrase.Type.TremoloLane,         PHRASE_TREMOLO_LANE },
            { MoonPhrase.Type.TrillLane,           PHRASE_TRILL_LANE },
            { MoonPhrase.Type.ProDrums_Activation, PHRASE_DRUM_FILL },
        };

        private static readonly List<MoonPhrase.Type> DrumsOnlySpecialPhrases = new()
        {
            MoonPhrase.Type.TremoloLane,
            MoonPhrase.Type.TrillLane,
            MoonPhrase.Type.ProDrums_Activation,
        };

        // Explicitly use \r\n here to ensure the parser handles all whitespace correctly
        private const string NEWLINE = "\r\n";

        private static void GenerateSongSection(MoonSong sourceSong, StringBuilder builder)
        {
            // Used only as a scoped guard here for the start/end of the section
            using var section = new ChartSection(builder, SECTION_SONG);

            // Write metadata values manually, shoving methods for it into ChartSection isn't really viable
            builder.Append($"  Resolution = {sourceSong.resolution}{NEWLINE}");
        }

        private static void GenerateSyncSection(MoonSong sourceSong, StringBuilder builder)
        {
            using var section = new ChartSection(builder, SECTION_SYNC_TRACK);

            var syncTrack = sourceSong.syncTrack;

            foreach (var bpm in syncTrack.Tempos)
            {
                uint writtenBpm = (uint) (bpm.BeatsPerMinute * 1000);
                section.AddEvent(bpm.Tick, "B", $"{writtenBpm}");
            }

            foreach (var ts in syncTrack.TimeSignatures)
            {
                section.AddEvent(ts.Tick, "TS", ts.Numerator, (uint) Math.Log2(ts.Denominator));
            }
        }

        private static void GenerateEventsSection(MoonSong sourceSong, StringBuilder builder)
        {
            using var section = new ChartSection(builder, SECTION_EVENTS);

            foreach (var ev in sourceSong.events.Concat(sourceSong.sections))
            {
                section.AddEvent(ev.tick, "E", $"\"{ev.text}\"");
            }
        }

        private static void GenerateInstrumentSection(MoonSong sourceSong, StringBuilder builder, MoonInstrument instrument, Difficulty difficulty)
        {
            // Skip unsupported instruments
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            if (!InstrumentToNoteLookupLookup.ContainsKey(gameMode))
                return;

            var chart = sourceSong.GetChart(instrument, difficulty);

            string instrumentName = InstrumentToNameLookup[instrument];
            string difficultyName = DifficultyToNameLookup[difficulty];

            using var section = new ChartSection(builder, $"{difficultyName}{instrumentName}");

            foreach (var note in chart.notes)
            {
                AppendNote(section, note, gameMode);
            }

            var textPhrases = new List<MoonText>();
            var phrasesToRemove = new List<MoonPhrase>();
            foreach (var phrase in chart.specialPhrases)
            {
                AppendPhrase(section, phrase, gameMode, phrasesToRemove, textPhrases);
            }

            foreach (var phrase in phrasesToRemove)
            {
                chart.Remove(phrase);
            }

            foreach (var text in chart.events.Concat(textPhrases))
            {
                section.AddEvent(text.tick, "E", text.text);
            }
        }

        private static void AppendNote(ChartSection section, MoonNote note, GameMode gameMode)
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
            int rawNote = gameMode switch
            {
                GameMode.Guitar => (int)note.guitarFret,
                GameMode.GHLGuitar => (int)note.ghliveGuitarFret,
                GameMode.Drums => (int)note.drumPad,

                _ => throw new NotSupportedException($".chart does not support game mode {gameMode}!")
            };

            uint chartNumber = noteLookup[rawNote];
            if (canDoubleKick && (flags & Flags.DoubleKick) != 0)
                chartNumber = NOTE_OFFSET_INSTRUMENT_PLUS;

            section.AddEvent(tick, "N", chartNumber, note.length);
            if (canForce && (flags & Flags.Forced) != 0)
                section.AddEvent(tick, "N", 5, 0);
            if (canTap && (flags & Flags.Tap) != 0)
                section.AddEvent(tick, "N", 6, 0);
            if (canCymbal && (flags & Flags.ProDrums_Cymbal) != 0)
                section.AddEvent(tick, "N", NOTE_OFFSET_PRO_DRUMS + chartNumber, 0);
            if (canDynamics && (flags & Flags.ProDrums_Accent) != 0)
                section.AddEvent(tick, "N", NOTE_OFFSET_DRUMS_ACCENT + chartNumber, 0);
            if (canDynamics && (flags & Flags.ProDrums_Ghost) != 0)
                section.AddEvent(tick, "N", NOTE_OFFSET_DRUMS_GHOST + chartNumber, 0);
        }

        private static void AppendPhrase(ChartSection section, MoonPhrase phrase, GameMode gameMode,
            List<MoonPhrase> phrasesToRemove, List<MoonText> textPhrases)
        {
            // Drums-only phrases
            if (gameMode is not GameMode.Drums && DrumsOnlySpecialPhrases.Contains(phrase.type))
            {
                phrasesToRemove.Add(phrase);
                return;
            }

            // Solos are written as text events in .chart
            if (phrase.type is MoonPhrase.Type.Solo)
            {
                // No need to worry about sorting here, `ChartSection` will already sort its events
                textPhrases.Add(new MoonText(SOLO_START, phrase.tick));
                textPhrases.Add(new MoonText(SOLO_END, phrase.tick + phrase.length));
                return;
            }

            uint phraseNumber = SpecialPhraseLookup[phrase.type];
            section.AddEvent(phrase.tick, "S", phraseNumber, phrase.length);
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
            YargLogger.AddLogListener(new DebugYargLogListener());

            var sourceSong = GenerateSong();
            string chartText = GenerateChartFile(sourceSong);
            MoonSong parsedSong;
            try
            {
                parsedSong = ChartReader.ReadFromText(chartText);
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