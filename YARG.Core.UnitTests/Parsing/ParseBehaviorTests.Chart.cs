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

    using ChartEventList = List<(uint tick, string data)>;

    public static class ChartEventListExtensions
    {
        public static void AddEvent(this ChartEventList events, uint tick, string typeCode, string data)
        {
            events.Add((tick, $"{typeCode} {data}"));
        }

        public static void AddEvent(this ChartEventList events, uint tick, string typeCode, uint value1)
        {
            events.Add((tick, $"{typeCode} {value1}"));
        }

        public static void AddEvent(this ChartEventList events, uint tick, string typeCode, uint value1, uint value2)
        {
            events.Add((tick, $"{typeCode} {value1} {value2}"));
        }
    }

    public class ChartParseBehaviorTests
    {
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
            WriteSectionHeader(builder, SECTION_SONG);

            builder.Append($"  Resolution = {sourceSong.resolution}{NEWLINE}");

            WriteSectionFooter(builder);
        }

        private static void GenerateSyncSection(MoonSong sourceSong, StringBuilder builder)
        {
            var section = new ChartEventList();

            var syncTrack = sourceSong.syncTrack;

            foreach (var bpm in syncTrack.Tempos)
            {
                uint writtenBpm = (uint) (bpm.BeatsPerMinute * 1000);
                section.AddEvent(bpm.Tick, "B", writtenBpm);
            }

            for (int i = 0; i < syncTrack.TimeSignatures.Count; i++)
            {
                var ts = syncTrack.TimeSignatures[i];
                if (ts.IsInterrupted)
                {
                    if (i == 0)
                    {
                        Assert.Fail($"Invalid interrupted time signature <{ts}>");
                        return;
                    }

                    var prevTs = syncTrack.TimeSignatures[i - 1];
                    Assert.Multiple(() =>
                    {
                        Assert.That(ts.Numerator, Is.EqualTo(prevTs.Numerator), "Interrupted time signatures must match the previous time signature");
                        Assert.That(ts.Denominator, Is.EqualTo(prevTs.Denominator), "Interrupted time signatures must match the previous time signature");
                    });
                    continue;
                }

                section.AddEvent(ts.Tick, "TS", ts.Numerator, (uint) Math.Log2(ts.Denominator));
            }

            // .chart does not store beatline information
            syncTrack.Beatlines.Clear();

            FinalizeSection(builder, SECTION_SYNC_TRACK, section);
        }

        private static void GenerateEventsSection(MoonSong sourceSong, StringBuilder builder)
        {
            var section = new ChartEventList();

            foreach (var ev in sourceSong.events.Concat(sourceSong.sections))
            {
                section.AddEvent(ev.tick, "E", '"' + ev.text + '"');
            }

            FinalizeSection(builder, SECTION_EVENTS, section);
        }

        private static void GenerateInstrumentSection(MoonSong sourceSong, StringBuilder builder, MoonInstrument instrument, Difficulty difficulty)
        {
            // Skip unsupported instruments
            var gameMode = MoonSong.InstrumentToChartGameMode(instrument);
            if (!InstrumentToNoteLookupLookup.ContainsKey(gameMode))
                return;

            var chart = sourceSong.GetChart(instrument, difficulty);

            var section = new ChartEventList();

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

            string instrumentName = InstrumentToNameLookup[instrument];
            string difficultyName = DifficultyToNameLookup[difficulty];
            string sectionName = $"{difficultyName}{instrumentName}";

            FinalizeSection(builder, sectionName, section);
        }

        private static void AppendNote(ChartEventList section, MoonNote note, GameMode gameMode)
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
                GameMode.Guitar => (int) note.guitarFret,
                GameMode.GHLGuitar => (int) note.ghliveGuitarFret,
                GameMode.Drums => (int) note.drumPad,

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

        private static void AppendPhrase(ChartEventList section, MoonPhrase phrase, GameMode gameMode,
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

        private static void WriteSectionHeader(StringBuilder builder, string name)
        {
            // [Name]\r\n
            // {\r\n
            builder.Append($"[{name}]{NEWLINE}");
            builder.Append($"{{{NEWLINE}");
        }

        private static void WriteSectionFooter(StringBuilder builder)
        {
            // }\r\n
            builder.Append($"}}{NEWLINE}");
        }

        private static void FinalizeSection(StringBuilder builder, string name, ChartEventList events)
        {
            events.Sort((left, right) =>
            {
                int compare = left.tick.CompareTo(right.tick);
                if (compare != 0)
                    return compare;

                return string.Compare(left.data, right.data, StringComparison.Ordinal);
            });

            WriteSectionHeader(builder, name);

            foreach (var (tick, data) in events)
            {
                builder.Append($"  {tick} = {data}{NEWLINE}");
            }

            WriteSectionFooter(builder);
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