using System;
using System.Collections.Generic;
using YARG.Core.Logging;
using YARG.Core.Utility;

namespace YARG.Core.Chart.Parsing
{
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;

    internal static partial class DotChartParser
    {
        private const Instrument DRUMS_INSTRUMENT = Instrument.FourLaneDrums;
        private const GameMode DRUMS_GAMEMODE = GameMode.FourLaneDrums;

        // List instead of a dictionary since we're enumerating instead of doing lookups
        private static readonly List<(string, Difficulty)> _difficultyLookup = new()
        {
            ( "Easy",   Difficulty.Easy ),
            ( "Medium", Difficulty.Medium ),
            ( "Hard",   Difficulty.Hard ),
            ( "Expert", Difficulty.Expert ),
        };

        private static readonly Dictionary<string, Instrument> _instrumentLookup = new()
        {
           { "Single",        Instrument.FiveFretGuitar },
           { "DoubleGuitar",  Instrument.FiveFretCoopGuitar },
           { "DoubleBass",    Instrument.FiveFretBass },
           { "DoubleRhythm",  Instrument.FiveFretRhythm },
           { "Keyboard",      Instrument.Keys },

           { "GHLGuitar", Instrument.SixFretGuitar },
           { "GHLCoop",   Instrument.SixFretCoopGuitar },
           { "GHLBass",   Instrument.SixFretBass },
           { "GHLRhythm", Instrument.SixFretRhythm },

           { "Drums", DRUMS_INSTRUMENT },
        };

        private static void ParseTrack(ReadOnlySpan<char> sectionName, AsciiTrimSplitter sectionBody,
            SongChart chart, in ParseSettings settings)
        {
            if (!TryGetInstrumentDifficultyForSection(sectionName, out var instrument, out var difficulty))
            {
                YargLogger.LogFormatDebug("Unknown .chart section: {0}", sectionName.ToString());
                return;
            }

            var gameMode = instrument.ToGameMode();
            DotChartSectionHandler handler = gameMode switch
            {
                GameMode.FiveFretGuitar or
                GameMode.SixFretGuitar => new DotChartGuitarHandler(instrument, difficulty, chart, settings),

                DRUMS_GAMEMODE => new DotChartDrumsHandler(difficulty, chart, settings),

                _ => throw new NotImplementedException($"Unhandled .chart instrument {instrument}!")
            };

            handler.ParseSection(sectionBody);
        }

        private static bool TryGetInstrumentDifficultyForSection(ReadOnlySpan<char> sectionName,
            out Instrument instrument, out Difficulty difficulty)
        {
            foreach (var (diffName, diff) in _difficultyLookup)
            {
                if (!sectionName.StartsWith(diffName))
                    continue;

                string instrumentName = sectionName[diffName.Length..].ToString();
                if (!_instrumentLookup.TryGetValue(instrumentName, out var inst))
                    continue;

                instrument = inst;
                difficulty = diff;
                return true;
            }

            instrument = default;
            difficulty = default;
            return false;
        }
    }
}