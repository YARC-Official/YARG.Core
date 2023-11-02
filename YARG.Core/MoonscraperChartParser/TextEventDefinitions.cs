using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song
{
    /// <summary>
    /// Constants for possible text events.
    /// </summary>
    internal static class TextEventDefinitions
    {
        #region Global lyric events
        public const string
        LYRIC_PREFIX = "lyric",
        EXPLICIT_LYRIC_PREFIX = "lyric_explicit",
        LYRIC_PREFIX_WITH_SPACE = LYRIC_PREFIX + " ",
        LYRIC_PHRASE_START = "phrase_start",
        LYRIC_PHRASE_END = "phrase_end";
        #endregion

        #region Solos
        public const string
        SOLO_START = "solo",
        SOLO_END = "soloend";
        #endregion

        #region Venue
        // NOTE: The definitions here are not how the events themselves are represented in the chart file.
        // They're re-interpretations meant to ease certain aspects of handling them later on.

        #region General
        public const string
        VENUE_OPTIONAL_EVENT_PREFIX = "optional",
        VENUE_OPTIONAL_EVENT_PREFIX_WITH_SPACE = VENUE_OPTIONAL_EVENT_PREFIX + " ";
        #endregion

        #region Performers
        public const string
        VENUE_PERFORMER_GUITAR = "guitar",
        VENUE_PERFORMER_BASS = "bass",
        VENUE_PERFORMER_DRUMS = "drums",
        VENUE_PERFORMER_VOCALS = "vocals",
        VENUE_PERFORMER_KEYS = "keys";
        #endregion

        #region Lighting
        // Keyframed
        public const string
        VENUE_LIGHTING_DEFAULT = "default",
        VENUE_LIGHTING_DISCHORD = "dischord",
        VENUE_LIGHTING_CHORUS = "chorus",
        VENUE_LIGHTING_COOL_MANUAL = "cool_manual", // manual_cool
        VENUE_LIGHTING_STOMP = "stomp",
        VENUE_LIGHTING_VERSE = "verse",
        VENUE_LIGHTING_WARM_MANUAL = "warm_manual", // manual_warm

        // Automatic
        VENUE_LIGHTING_BIG_ROCK_ENDING = "big_rock_ending", // bre
        VENUE_LIGHTING_BLACKOUT_FAST = "blackout_fast",
        VENUE_LIGHTING_BLACKOUT_SLOW = "blackout_slow",
        VENUE_LIGHTING_BLACKOUT_SPOTLIGHT = "blackout_spotlight", // blackout_spot
        VENUE_LIGHTING_COOL_AUTOMATIC = "cool_automatic", // loop_cool
        VENUE_LIGHTING_FLARE_FAST = "flare_fast",
        VENUE_LIGHTING_FLARE_SLOW = "flare_slow",
        VENUE_LIGHTING_FRENZY = "frenzy",
        VENUE_LIGHTING_INTRO = "intro",
        VENUE_LIGHTING_HARMONY = "harmony",
        VENUE_LIGHTING_SILHOUETTES = "silhouettes",
        VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT = "silhouettes_spotlight", // silhouettes_spot
        VENUE_LIGHTING_SEARCHLIGHTS = "searchlights",
        VENUE_LIGHTING_STROBE_FAST = "strobe_fast",
        VENUE_LIGHTING_STROBE_SLOW = "strobe_slow",
        VENUE_LIGHTING_SWEEP = "sweep",
        VENUE_LIGHTING_WARM_AUTOMATIC = "warm_automatic", // loop_warm

        // Keyframe events
        VENUE_LIGHTING_FIRST = "first",
        VENUE_LIGHTING_NEXT = "next",
        VENUE_LIGHTING_PREVIOUS = "previous";
        #endregion

        #region Post-processing
        public const string
        VENUE_POSTPROCESS_DEFAULT = "default", // ProFilm_a.pp

        // Basic effects
        VENUE_POSTPROCESS_BLOOM = "bloom", // bloom.pp
        VENUE_POSTPROCESS_BRIGHT = "bright", // bright.pp
        VENUE_POSTPROCESS_CONTRAST = "contrast", // film_contrast.pp
        VENUE_POSTPROCESS_MIRROR = "mirror", // ProFilm_mirror_a.pp
        VENUE_POSTPROCESS_PHOTONEGATIVE = "photonegative", // photo_negative.pp
        VENUE_POSTPROCESS_POSTERIZE = "posterize", // posterize.pp

        // Color filters/effects
        VENUE_POSTPROCESS_BLACK_WHITE = "black_white", // film_b+w.pp
        VENUE_POSTPROCESS_SEPIATONE = "sepiatone", // film_sepia_ink.pp
        VENUE_POSTPROCESS_SILVERTONE = "silvertone", // film_silvertone.pp

        VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE = "choppy_black_white", // photocopy.pp
        VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK = "photonegative_red_black", // horror_movie_special.pp
        VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE = "polarized_black_white", // contrast_a.pp
        VENUE_POSTPROCESS_POLARIZED_RED_BLUE = "polarized_red_blue", // ProFilm_psychedelic_blue_red.pp

        VENUE_POSTPROCESS_DESATURATED_RED = "desaturated_red", // ProFilm_b.pp
        VENUE_POSTPROCESS_DESATURATED_BLUE = "desaturated_blue", // desat_blue.pp

        VENUE_POSTPROCESS_CONTRAST_RED = "contrast_red", // film_contrast_red.pp
        VENUE_POSTPROCESS_CONTRAST_GREEN = "contrast_green", // film_contrast_green.pp
        VENUE_POSTPROCESS_CONTRAST_BLUE = "contrast_blue", // film_contrast_blue.pp

        // Grainy
        VENUE_POSTPROCESS_GRAINY_FILM = "grainy_film", // film_16mm.pp
        VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION = "grainy_chromatic_abberation", // shitty_tv.pp

        // Scanlines
        VENUE_POSTPROCESS_SCANLINES = "scanlines", // video_a.pp
        VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE = "scanlines_black_white", // video_bw.pp
        VENUE_POSTPROCESS_SCANLINES_BLUE = "scanlines_blue", // film_blue_filter.pp
        VENUE_POSTPROCESS_SCANLINES_SECURITY = "scanlines_security", // video_security.pp

        // Trails (video feed delay, a "visual echo")
        VENUE_POSTPROCESS_TRAILS = "trails", // clean_trails.pp
        VENUE_POSTPROCESS_TRAILS_LONG = "trails_long", // video_trails.pp
        VENUE_POSTPROCESS_TRAILS_DESATURATED = "trails_desaturated", // desat_posterize_trails.pp
        VENUE_POSTPROCESS_TRAILS_FLICKERY = "trails_flickery", // flicker_trails.pp
        VENUE_POSTPROCESS_TRAILS_SPACEY = "trails_spacey"; // space_woosh.pp
        #endregion

        #region Stage effects
        public const string
        VENUE_STAGE_BONUS_FX = "bonus_fx",
        VENUE_STAGE_FOG_ON = "fog_on",
        VENUE_STAGE_FOG_OFF = "fog_off";
        #endregion

        #endregion // Venue
    }
}
