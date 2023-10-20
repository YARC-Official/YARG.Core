// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song.IO
{
    internal static class ChartIOHelper
    {
        public const string SECTION_SONG = "Song";
        public const string SECTION_SYNC_TRACK = "SyncTrack";
        public const string SECTION_EVENTS = "Events";

        // See MidIOHelper for regex details
        public static readonly Regex TextEventRegex = MidIOHelper.TextEventRegex;
        public static readonly Regex SectionEventRegex = MidIOHelper.SectionEventRegex;


        public const int NOTE_OFFSET_PRO_DRUMS = 64;
        public const int NOTE_OFFSET_INSTRUMENT_PLUS = 32;
        public const int NOTE_OFFSET_DRUMS_ACCENT = 33;
        public const int NOTE_OFFSET_DRUMS_GHOST = 39;

        public const int PHRASE_VERSUS_PLAYER_1 = 0;
        public const int PHRASE_VERSUS_PLAYER_2 = 1;
        public const int PHRASE_STARPOWER = 2;
        public const int PHRASE_DRUM_FILL = 64;
        public const int PHRASE_TREMOLO_LANE = 65;
        public const int PHRASE_TRILL_LANE = 66;

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

        public static float GetHopoThreshold(ParseSettings settings, float resolution)
        {
            // With a 192 resolution, .chart has a HOPO threshold of 65 ticks, not 64,
            // so we need to scale this factor to different resolutions (480 res = 162.5 threshold)
            // This extra tick is meant for some slight leniency, .mid has it too but it's applied
            // after factoring in the resolution there, not before
            const float DEFAULT_RESOLUTION = 192;
            const float HOPO_THRESHOLD_FACTOR = ((DEFAULT_RESOLUTION / 3) + 1) / DEFAULT_RESOLUTION;
            const float EIGHTHNOTE_HOPO_THRESHOLD_FACTOR = ((DEFAULT_RESOLUTION / 2) + 1) / DEFAULT_RESOLUTION;

            // Prefer explicit tick value to eighth-note HOPO value
            if (settings.HopoThreshold >= 0)
                return settings.HopoThreshold;
            else if (settings.EighthNoteHopo)
                return resolution * EIGHTHNOTE_HOPO_THRESHOLD_FACTOR;
            else
                return resolution * HOPO_THRESHOLD_FACTOR;
        }
    }
}
