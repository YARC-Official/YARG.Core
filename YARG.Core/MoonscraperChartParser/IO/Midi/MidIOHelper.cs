// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace MoonscraperChartEditor.Song.IO
{
    public static class MidIOHelper
    {
        // Track names
        public const string BEAT_TRACK = "BEAT";
        public const string EVENTS_TRACK = "EVENTS";
        public const string VENUE_TRACK = "VENUE";
        public const string GUITAR_TRACK = "PART GUITAR";
        public const string GH1_GUITAR_TRACK = "T1 GEMS";
        public const string GUITAR_COOP_TRACK = "PART GUITAR COOP";
        public const string BASS_TRACK = "PART BASS";
        public const string RHYTHM_TRACK = "PART RHYTHM";
        public const string KEYS_TRACK = "PART KEYS";
        public const string DRUMS_TRACK = "PART DRUMS";
        public const string DRUMS_REAL_TRACK = "PART REAL_DRUMS_PS";
        public const string GHL_GUITAR_TRACK = "PART GUITAR GHL";
        public const string GHL_BASS_TRACK = "PART BASS GHL";
        public const string GHL_RHYTHM_TRACK = "PART RHYTHM GHL";
        public const string GHL_GUITAR_COOP_TRACK = "PART GUITAR COOP GHL";
        public const string VOCALS_TRACK = "PART VOCALS";

        // Note numbers
        public const byte DOUBLE_KICK_NOTE = 95;
        public const byte SOLO_NOTE = 103;                 // http://docs.c3universe.com/rbndocs/index.php?title=Guitar_and_Bass_Authoring#Solo_Sections
        public const byte TAP_NOTE_CH = 104;               // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/5-Fret Guitar.md
        public const byte VERSUS_PHRASE_PLAYER_1 = 105;    // Guitar Hero 2 and Rock Band 1/2 use these to mark phrases for face-off
        public const byte VERSUS_PHRASE_PLAYER_2 = 106;    // and other competitive modes where the players trade off phrases of notes
        public const byte LYRICS_PHRASE_1 = VERSUS_PHRASE_PLAYER_1; // These are also used to mark phrases on vocals
        public const byte LYRICS_PHRASE_2 = VERSUS_PHRASE_PLAYER_2; // Rock Band 3 dropped these versus phrases however, and on vocals just uses note 105
        public const byte FLAM_MARKER = 109;
        public const byte STARPOWER_NOTE = 116;            // http://docs.c3universe.com/rbndocs/index.php?title=Overdrive_and_Big_Rock_Endings

        // http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring#Drum_Fills
        public const byte DRUM_FILL_NOTE_0 = 120;
        public const byte DRUM_FILL_NOTE_1 = 121;
        public const byte DRUM_FILL_NOTE_2 = 122;
        public const byte DRUM_FILL_NOTE_3 = 123;
        public const byte DRUM_FILL_NOTE_4 = 124;

        // Drum rolls - http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring#Drum_Rolls
        public const byte TREMOLO_LANE_NOTE = 126;
        public const byte TRILL_LANE_NOTE = 127;

        // Text events
        public const string SOLO_EVENT_TEXT = "solo";
        public const string SOLO_END_EVENT_TEXT = "soloend";

        public const string LYRIC_EVENT_PREFIX = ChartIOHelper.LYRIC_EVENT_PREFIX;
        public const string LYRICS_PHRASE_START_TEXT = ChartIOHelper.EVENT_PHRASE_START;
        public const string LYRICS_PHRASE_END_TEXT = ChartIOHelper.EVENT_PHRASE_END;

        public const string SECTION_PREFIX_RB2 = "section ";
        public const string SECTION_PREFIX_RB3 = "prc_";

        // These events are valid both with and without brackets.
        // The bracketed versions follow the style of other existing .mid text events.
        public const string CHART_DYNAMICS_TEXT = "ENABLE_CHART_DYNAMICS";
        public const string CHART_DYNAMICS_TEXT_BRACKET = "[ENABLE_CHART_DYNAMICS]";
        public const string ENHANCED_OPENS_TEXT = "ENHANCED_OPENS";
        public const string ENHANCED_OPENS_TEXT_BRACKET = "[ENHANCED_OPENS]";

        // Note velocities
        public const byte VELOCITY = 100;             // default note velocity for exporting
        public const byte VELOCITY_ACCENT = 127;      // fof/ps
        public const byte VELOCITY_GHOST = 1;         // fof/ps

        // Lookup tables
        public static readonly Dictionary<string, MoonSong.MoonInstrument> TrackNameToInstrumentMap = new()
        {
            { GUITAR_TRACK,        MoonSong.MoonInstrument.Guitar },
            { GH1_GUITAR_TRACK,    MoonSong.MoonInstrument.Guitar },
            { GUITAR_COOP_TRACK,   MoonSong.MoonInstrument.GuitarCoop },
            { BASS_TRACK,          MoonSong.MoonInstrument.Bass },
            { RHYTHM_TRACK,        MoonSong.MoonInstrument.Rhythm },
            { KEYS_TRACK,          MoonSong.MoonInstrument.Keys },
            { DRUMS_TRACK,         MoonSong.MoonInstrument.Drums },
            { DRUMS_REAL_TRACK,    MoonSong.MoonInstrument.Drums },
            { GHL_GUITAR_TRACK,    MoonSong.MoonInstrument.GHLiveGuitar },
            { GHL_BASS_TRACK,      MoonSong.MoonInstrument.GHLiveBass },
            { GHL_RHYTHM_TRACK,    MoonSong.MoonInstrument.GHLiveRhythm },
            { GHL_GUITAR_COOP_TRACK, MoonSong.MoonInstrument.GHLiveCoop },
        };

        public static readonly Dictionary<MoonSong.Difficulty, int> GUITAR_DIFF_START_LOOKUP = new()
        {
            { MoonSong.Difficulty.Easy, 60 },
            { MoonSong.Difficulty.Medium, 72 },
            { MoonSong.Difficulty.Hard, 84 },
            { MoonSong.Difficulty.Expert, 96 }
        };

        public static readonly Dictionary<MoonSong.Difficulty, int> GHL_GUITAR_DIFF_START_LOOKUP = new()
        {
            { MoonSong.Difficulty.Easy, 58 },
            { MoonSong.Difficulty.Medium, 70 },
            { MoonSong.Difficulty.Hard, 82 },
            { MoonSong.Difficulty.Expert, 94 }
        };

        public static readonly Dictionary<MoonSong.Difficulty, int> DRUMS_DIFF_START_LOOKUP = new()
        {
            { MoonSong.Difficulty.Easy, 60 },
            { MoonSong.Difficulty.Medium, 72 },
            { MoonSong.Difficulty.Hard, 84 },
            { MoonSong.Difficulty.Expert, 96 }
        };

        // http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring
        public static readonly Dictionary<MoonNote.DrumPad, int> PAD_TO_CYMBAL_LOOKUP = new()
        {
            { MoonNote.DrumPad.Yellow, 110 },
            { MoonNote.DrumPad.Blue, 111 },
            { MoonNote.DrumPad.Orange, 112 },
        };

        public static readonly Dictionary<int, MoonNote.DrumPad> CYMBAL_TO_PAD_LOOKUP = PAD_TO_CYMBAL_LOOKUP.ToDictionary((i) => i.Value, (i) => i.Key);
    }
}
