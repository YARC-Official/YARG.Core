// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MoonscraperChartEditor.Song.IO
{
    public static class ChartIOHelper
    {
        public const string SECTION_SONG = "[Song]";
        public const string SECTION_SYNC_TRACK = "[SyncTrack]";
        public const string SECTION_EVENTS = "[Events]";

        public const string LYRIC_EVENT_PREFIX = "lyric ";
        public const string EVENT_PHRASE_START = "phrase_start";
        public const string EVENT_PHRASE_END = "phrase_end";

        public const int NOTE_OFFSET_PRO_DRUMS = 64;
        public const int NOTE_OFFSET_INSTRUMENT_PLUS = 32;
        public const int NOTE_OFFSET_DRUMS_ACCENT = 33;
        public const int NOTE_OFFSET_DRUMS_GHOST = 39;

        public const int PHRASE_STARPOWER = 2;
        public const int PHRASE_DRUM_FILL = 64;
        public const int PHRASE_DRUM_ROLL_SINGLE = 65;
        public const int PHRASE_DRUM_ROLL_DOUBLE = 66;

        public enum TrackLoadType
        {
            Guitar,
            Drums,
            GHLiveGuitar,

            Unrecognised
        }

        public static readonly Dictionary<int, int> GuitarNoteNumLookup = new()
        {
            { 0, (int)MoonNote.GuitarFret.Green     },
            { 1, (int)MoonNote.GuitarFret.Red       },
            { 2, (int)MoonNote.GuitarFret.Yellow    },
            { 3, (int)MoonNote.GuitarFret.Blue      },
            { 4, (int)MoonNote.GuitarFret.Orange    },
            { 7, (int)MoonNote.GuitarFret.Open      },
        };

        public static readonly Dictionary<int, MoonNote.Flags> GuitarFlagNumLookup = new()
        {
            { 5      , MoonNote.Flags.Forced },
            { 6      , MoonNote.Flags.Tap },
        };

        public static readonly Dictionary<int, int> DrumNoteNumLookup = new()
        {
            { 0, (int)MoonNote.DrumPad.Kick      },
            { 1, (int)MoonNote.DrumPad.Red       },
            { 2, (int)MoonNote.DrumPad.Yellow    },
            { 3, (int)MoonNote.DrumPad.Blue      },
            { 4, (int)MoonNote.DrumPad.Orange    },
            { 5, (int)MoonNote.DrumPad.Green     },
        };

        // Default flags for drums notes
        public static readonly Dictionary<int, MoonNote.Flags> DrumNoteDefaultFlagsLookup = new()
        {
            { (int)MoonNote.DrumPad.Kick      , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Red       , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Yellow    , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Blue      , MoonNote.Flags.None },
            { (int)MoonNote.DrumPad.Orange    , MoonNote.Flags.None },   // Orange becomes green during 4-lane
            { (int)MoonNote.DrumPad.Green     , MoonNote.Flags.None },
        };

        public static readonly Dictionary<int, int> GhlNoteNumLookup = new()
        {
            { 0, (int)MoonNote.GHLiveGuitarFret.White1    },
            { 1, (int)MoonNote.GHLiveGuitarFret.White2    },
            { 2, (int)MoonNote.GHLiveGuitarFret.White3    },
            { 3, (int)MoonNote.GHLiveGuitarFret.Black1    },
            { 4, (int)MoonNote.GHLiveGuitarFret.Black2    },
            { 8, (int)MoonNote.GHLiveGuitarFret.Black3    },
            { 7, (int)MoonNote.GHLiveGuitarFret.Open      },
        };

        public static readonly Dictionary<int, MoonNote.Flags> GhlFlagNumLookup = GuitarFlagNumLookup;

        public static readonly Dictionary<string, MoonSong.Difficulty> TrackNameToTrackDifficultyLookup = new()
        {
            { "Easy",   MoonSong.Difficulty.Easy    },
            { "Medium", MoonSong.Difficulty.Medium  },
            { "Hard",   MoonSong.Difficulty.Hard    },
            { "Expert", MoonSong.Difficulty.Expert  },
        };

        public static readonly Dictionary<string, MoonSong.MoonInstrument> InstrumentStrToEnumLookup = new()
        {
            { "Single",         MoonSong.MoonInstrument.Guitar },
            { "DoubleGuitar",   MoonSong.MoonInstrument.GuitarCoop },
            { "DoubleBass",     MoonSong.MoonInstrument.Bass },
            { "DoubleRhythm",   MoonSong.MoonInstrument.Rhythm },
            { "Drums",          MoonSong.MoonInstrument.Drums },
            { "Keyboard",       MoonSong.MoonInstrument.Keys },
            { "GHLGuitar",      MoonSong.MoonInstrument.GHLiveGuitar },
            { "GHLBass",        MoonSong.MoonInstrument.GHLiveBass },
            { "GHLRhythm",      MoonSong.MoonInstrument.GHLiveRhythm },
            { "GHLCoop",        MoonSong.MoonInstrument.GHLiveCoop },
        };

        public static readonly Dictionary<MoonSong.MoonInstrument, TrackLoadType> InstrumentParsingTypeLookup = new()
        {
            // Other instruments default to loading as a guitar type track
            { MoonSong.MoonInstrument.Drums, TrackLoadType.Drums },
            { MoonSong.MoonInstrument.GHLiveGuitar, TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveBass,  TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveRhythm,  TrackLoadType.GHLiveGuitar },
            { MoonSong.MoonInstrument.GHLiveCoop,  TrackLoadType.GHLiveGuitar },
        };

        public static class MetaData
        {
            private const string QUOTEVALIDATE = @"""[^""\\]*(?:\\.[^""\\]*)*""";
            private const string QUOTESEARCH = "\"([^\"]*)\"";
            private const string FLOATSEARCH = @"[\-\+]?\d+(\.\d+)?";       // US culture only

            public static readonly CultureInfo FormatCulture = new("en-US");

            public enum MetadataValueType
            {
                String,
                Float,
                Player2,
                Difficulty,
                Year,
            }

            public class MetadataItem
            {
                private readonly string m_key;
                private readonly Regex m_readerParseRegex;

                public string key => m_key;
                public Regex regex => m_readerParseRegex;

                public MetadataItem(string key, MetadataValueType type)
                {
                    m_key = key;
                    m_readerParseRegex = type switch
                    {
                        MetadataValueType.String => new Regex(key + " = " + QUOTEVALIDATE, RegexOptions.Compiled),
                        MetadataValueType.Float => new Regex(key + " = " + FLOATSEARCH, RegexOptions.Compiled),
                        MetadataValueType.Player2 => new Regex(key + @" = \w+", RegexOptions.Compiled),
                        MetadataValueType.Difficulty => new Regex(key + @" = \d+", RegexOptions.Compiled),
                        MetadataValueType.Year => new Regex(key + " = " + QUOTEVALIDATE, RegexOptions.Compiled),
                        _ => throw new System.Exception("Unhandled Metadata item type")
                    };
                }
            }

            public static readonly MetadataItem name = new("Name", MetadataValueType.String);
            public static readonly MetadataItem artist = new("Artist", MetadataValueType.String);
            public static readonly MetadataItem charter = new("Charter", MetadataValueType.String);
            public static readonly MetadataItem offset = new("Offset", MetadataValueType.Float);
            public static readonly MetadataItem resolution = new("Resolution", MetadataValueType.Float);
            public static readonly MetadataItem difficulty = new("Difficulty", MetadataValueType.Difficulty);
            public static readonly MetadataItem length = new("Length", MetadataValueType.Float);
            public static readonly MetadataItem previewStart = new("PreviewStart", MetadataValueType.Float);
            public static readonly MetadataItem previewEnd = new("PreviewEnd", MetadataValueType.Float);
            public static readonly MetadataItem genre = new("Genre", MetadataValueType.String);
            public static readonly MetadataItem year = new("Year", MetadataValueType.Year);
            public static readonly MetadataItem album = new("Album", MetadataValueType.String);

            public static string ParseAsString(string line)
            {
                return Regex.Matches(line, QUOTESEARCH)[0].ToString().Trim('"');
            }

            public static float ParseAsFloat(string line)
            {
                return float.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString(), FormatCulture);  // .chart format only allows '.' as decimal seperators. Need to parse correctly under any locale.
            }

            public static short ParseAsShort(string line)
            {
                return short.Parse(Regex.Matches(line, FLOATSEARCH)[0].ToString());
            }
        }

        public class NoteFlagPriority
        {
            // Flags to skip adding if the corresponding flag is already present
            private static readonly Dictionary<MoonNote.Flags, MoonNote.Flags> NoteBlockingFlagsLookup = new()
            {
                { MoonNote.Flags.Forced, MoonNote.Flags.Tap },
                { MoonNote.Flags.ProDrums_Ghost, MoonNote.Flags.ProDrums_Accent },
            };

            // Flags to remove if the corresponding flag is being added
            private static readonly Dictionary<MoonNote.Flags, MoonNote.Flags> NoteFlagsToRemoveLookup =
                NoteBlockingFlagsLookup.ToDictionary((i) => i.Value, (i) => i.Key);

            public static readonly NoteFlagPriority Forced = new(MoonNote.Flags.Forced);
            public static readonly NoteFlagPriority Tap = new(MoonNote.Flags.Tap);
            public static readonly NoteFlagPriority InstrumentPlus = new(MoonNote.Flags.InstrumentPlus);
            public static readonly NoteFlagPriority Cymbal = new(MoonNote.Flags.ProDrums_Cymbal);
            public static readonly NoteFlagPriority Accent = new(MoonNote.Flags.ProDrums_Accent);
            public static readonly NoteFlagPriority Ghost = new(MoonNote.Flags.ProDrums_Ghost);

            private static readonly List<NoteFlagPriority> priorities = new()
            {
                Forced,
                Tap,
                InstrumentPlus,
                Cymbal,
                Accent,
                Ghost,
            };

            public MoonNote.Flags flagToAdd { get; } = MoonNote.Flags.None;
            public MoonNote.Flags blockingFlag { get; } = MoonNote.Flags.None;
            public MoonNote.Flags flagToRemove { get; } = MoonNote.Flags.None;

            public NoteFlagPriority(MoonNote.Flags flag)
            {
                flagToAdd = flag;

                if (NoteBlockingFlagsLookup.TryGetValue(flagToAdd, out var blockingFlag))
                {
                    this.blockingFlag = blockingFlag;
                }

                if (NoteFlagsToRemoveLookup.TryGetValue(flagToAdd, out var flagToRemove))
                {
                    this.flagToRemove = flagToRemove;
                }
            }

            public bool TryApplyToNote(MoonNote moonNote)
            {
                // Don't add if the flag to be added is lower-priority than a conflicting, already-added flag
                if (blockingFlag != MoonNote.Flags.None && moonNote.flags.HasFlag(blockingFlag))
                {
                    return false;
                }

                // Flag can be added without issue
                moonNote.flags |= flagToAdd;

                // Remove flags that are lower-priority than the added flag
                if (flagToRemove != MoonNote.Flags.None && moonNote.flags.HasFlag(flagToRemove))
                {
                    moonNote.flags &= ~flagToRemove;
                }

                return true;
            }

            public bool AreFlagsValid(MoonNote.Flags flags)
            {
                if (flagToAdd == MoonNote.Flags.None)
                {
                    // No flag to validate against
                    return true;
                }

                if (blockingFlag != MoonNote.Flags.None)
                {
                    if (flags.HasFlag(blockingFlag) && flags.HasFlag(flagToAdd))
                    {
                        // Note has conflicting flags
                        return false;
                    }
                }

                if (flagToRemove != MoonNote.Flags.None)
                {
                    if (flags.HasFlag(flagToAdd) && flags.HasFlag(flagToRemove))
                    {
                        // Note has conflicting flags
                        return false;
                    }
                }

                return true;
            }

            public static bool AreFlagsValidForAll(MoonNote.Flags flags, out NoteFlagPriority invalidPriority)
            {
                foreach (var priority in priorities)
                {
                    if (!priority.AreFlagsValid(flags))
                    {
                        invalidPriority = priority;
                        return false;
                    }
                }

                invalidPriority = null;
                return true;
            }
        }
    }
}
