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
        public const string PRO_GUITAR_17_FRET_TRACK = "PART REAL_GUITAR";
        public const string PRO_GUITAR_22_FRET_TRACK = "PART REAL_GUITAR_22";
        public const string PRO_BASS_17_FRET_TRACK = "PART REAL_BASS";
        public const string PRO_BASS_22_FRET_TRACK = "PART REAL_BASS_22";
        public const string DRUMS_TRACK = "PART DRUMS";
        public const string DRUMS_REAL_TRACK = "PART REAL_DRUMS_PS";
        public const string GHL_GUITAR_TRACK = "PART GUITAR GHL";
        public const string GHL_BASS_TRACK = "PART BASS GHL";
        public const string GHL_RHYTHM_TRACK = "PART RHYTHM GHL";
        public const string GHL_GUITAR_COOP_TRACK = "PART GUITAR COOP GHL";
        public const string VOCALS_TRACK = "PART VOCALS";
        public const string HARMONY_1_TRACK = "HARM1";
        public const string HARMONY_2_TRACK = "HARM2";
        public const string HARMONY_3_TRACK = "HARM3";
        // The Beatles: Rock Band uses these instead for its harmony tracks
        public const string HARMONY_1_TRACK_2 = "PART HARM1";
        public const string HARMONY_2_TRACK_2 = "PART HARM2";
        public const string HARMONY_3_TRACK_2 = "PART HARM3";

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

        // Pro Guitar notes
        public const byte SOLO_NOTE_PRO_GUITAR = 115;

        // Vocals notes
        public const byte LYRIC_SHIFT_NOTE = 0;
        public const byte RANGE_SHIFT_NOTE = 1;
        public const byte VOCALS_RANGE_START = 36;
        public const byte VOCALS_RANGE_END = 84;
        public const byte PERCUSSION_NOTE = 96;
        public const byte NONPLAYED_PERCUSSION_NOTE = 97;

        // Pro Guitar channels
        public const byte PRO_GUITAR_CHANNEL_NORMAL = 0;
        public const byte PRO_GUITAR_CHANNEL_GHOST = 1;
        public const byte PRO_GUITAR_CHANNEL_BEND = 2;
        public const byte PRO_GUITAR_CHANNEL_MUTED = 3;
        public const byte PRO_GUITAR_CHANNEL_TAP = 4;
        public const byte PRO_GUITAR_CHANNEL_HARMONIC = 5;
        public const byte PRO_GUITAR_CHANNEL_PINCH_HARMONIC = 6;

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
            { PRO_GUITAR_17_FRET_TRACK, MoonSong.MoonInstrument.ProGuitar_17Fret },
            { PRO_GUITAR_22_FRET_TRACK, MoonSong.MoonInstrument.ProGuitar_22Fret },
            { PRO_BASS_17_FRET_TRACK,   MoonSong.MoonInstrument.ProBass_17Fret },
            { PRO_BASS_22_FRET_TRACK,   MoonSong.MoonInstrument.ProBass_22Fret },
            { VOCALS_TRACK,        MoonSong.MoonInstrument.Vocals },
            { HARMONY_1_TRACK,     MoonSong.MoonInstrument.Harmony1 },
            { HARMONY_2_TRACK,     MoonSong.MoonInstrument.Harmony2 },
            { HARMONY_3_TRACK,     MoonSong.MoonInstrument.Harmony3 },
            { HARMONY_1_TRACK_2,   MoonSong.MoonInstrument.Harmony1 },
            { HARMONY_2_TRACK_2,   MoonSong.MoonInstrument.Harmony2 },
            { HARMONY_3_TRACK_2,   MoonSong.MoonInstrument.Harmony3 },
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

        public static readonly Dictionary<MoonSong.Difficulty, int> PRO_GUITAR_DIFF_START_LOOKUP = new()
        {
            { MoonSong.Difficulty.Easy, 24 },
            { MoonSong.Difficulty.Medium, 48 },
            { MoonSong.Difficulty.Hard, 72 },
            { MoonSong.Difficulty.Expert, 96 }
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

        public static readonly Dictionary<byte, MoonNote.Flags> PRO_GUITAR_CHANNEL_FLAG_LOOKUP = new()
        {
            // Not all flags are implemented yet
            { PRO_GUITAR_CHANNEL_NORMAL,         MoonNote.Flags.None },
            // { PRO_GUITAR_CHANNEL_GHOST,          MoonNote.Flags. },
            // { PRO_GUITAR_CHANNEL_BEND,           MoonNote.Flags. },
            { PRO_GUITAR_CHANNEL_MUTED,          MoonNote.Flags.ProGuitar_Muted },
            // { PRO_GUITAR_CHANNEL_TAP,            MoonNote.Flags. },
            // { PRO_GUITAR_CHANNEL_HARMONIC,       MoonNote.Flags. },
            // { PRO_GUITAR_CHANNEL_PINCH_HARMONIC, MoonNote.Flags. },
        };
    }
}
