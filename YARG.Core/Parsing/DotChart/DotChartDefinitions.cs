using System;
using System.Collections.Generic;

namespace YARG.Core.Parsing
{
    /// <summary>
    /// Note numbers used on 5-fret guitar tracks.
    /// </summary>
    public enum DotChartFiveFretGuitarNotes
    {
        Green = 0,
        Red = 1,
        Yellow = 2,
        Blue = 3,
        Orange = 4,

        Open = 7,
    }

    /// <summary>
    /// Note numbers used on 6-fret guitar tracks.
    /// </summary>
    public enum DotChartSixFretGuitarNotes
    {
        White1 = 0,
        White2 = 1,
        White3 = 2,
        Black1 = 3,
        Black2 = 4,
        Black3 = 8,

        Open = 7,
    }

    /// <summary>
    /// Note flags used on guitar tracks.
    /// </summary>
    public enum DotChartGuitarFlags
    {
        Forced = 5,
        Tap = 6,
    }

    /// <summary>
    /// Note numbers used on drums tracks.
    /// </summary>
    /// <remarks>
    /// Orange and Green refer to the nominal values in the chart file,
    /// while Four/FiveLaneGreen refer to what should be used for their respective mode.
    /// </remarks>
    public enum DotChartDrumsNotes
    {
        Kick = 0,
        DoubleKick = 32,

        Red = 1,
        Yellow = 2,
        Blue = 3,
        Orange = 4,
        Green = 5,

        FourLaneGreen = Orange,
        FiveLaneGreen = Green,
    }

    /// <summary>
    /// Note flags used on drums tracks.
    /// </summary>
    /// <remarks>
    /// Orange and Green refer to the nominal values in the chart file,
    /// while Four/FiveLaneGreen refer to what should be used for their respective mode.
    /// </remarks>
    public enum DotChartDrumsFlags
    {
        RedAccent = 34,
        YellowAccent = 35,
        BlueAccent = 36,
        OrangeAccent = 37,
        GreenAccent = 38,

        FourLaneGreenAccent = OrangeAccent,
        FiveLaneGreenAccent = GreenAccent,

        RedGhost = 40,
        YellowGhost = 41,
        BlueGhost = 42,
        OrangeGhost = 43,
        GreenGhost = 34,

        FourLaneGreenGhost = OrangeGhost,
        FiveLaneGreenGhost = GreenGhost,

        YellowCymbal = 66,
        BlueCymbal = 67,
        OrangeCymbal = 68,

        FourLaneGreenCymbal = OrangeCymbal,
    }

    /// <summary>
    /// Phrases used on instrument tracks.
    /// </summary>
    public enum DotChartPhraseTypes
    {
        Versus_Player1 = 0,
        Versus_Player2 = 1,

        StarPower = 2,

        BigRockEnding = 64,
        TremoloLane = 65,
        TrillLane = 66,

        Drums_StarPowerActivation = BigRockEnding,
        Drums_SingleRoll = TremoloLane,
        Drums_DoubleRoll = TrillLane,
    }

    /// <summary>
    /// Sections that can be found in .chart files, and what instrument/difficulty they correspond to.
    /// </summary>
    public static class DotChartSections
    {
        public const string SONG_SECTION = "Song";
        public const string SYNC_TRACK_SECTION = "SyncTrack";
        public const string EVENTS_SECTION = "Events";

        private static readonly Dictionary<string, Difficulty> _difficultyLookup = new()
        {
            { "Easy",   Difficulty.Easy   },
            { "Medium", Difficulty.Medium },
            { "Hard",   Difficulty.Hard   },
            { "Expert", Difficulty.Expert },
        };

        public static IReadOnlyDictionary<string, Difficulty> DifficultyLookup => _difficultyLookup;

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

            { "Drums", Instrument.FourLaneDrums },
        };

        public static IReadOnlyDictionary<string, Instrument> InstrumentLookup => _instrumentLookup;

        public static bool TryGetInstrumentDifficultyForSection(ReadOnlySpan<char> sectionName, out Instrument instrument,
            out Difficulty difficulty)
        {
            foreach (var (diffName, diff) in _difficultyLookup)
            {
                if (!sectionName.StartsWith(diffName))
                    continue;

                foreach (var (instName, inst) in _instrumentLookup)
                {
                    if (!sectionName.EndsWith(instName))
                        continue;

                    instrument = inst;
                    difficulty = diff;
                    return true;
                }
            }

            instrument = default;
            difficulty = default;
            return false;
        }
    }
}