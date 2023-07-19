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
        VENUE_CHARACTER_GUITAR = "guitar",
        VENUE_CHARACTER_BASS = "bass",
        VENUE_CHARACTER_DRUMS = "drums",
        VENUE_CHARACTER_VOCALS = "vocals",
        VENUE_CHARACTER_KEYS = "keys";
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
        VENUE_POSTPROCESS_BASIC_BLOOM = "basic_bloom", // bloom.pp
        VENUE_POSTPROCESS_BASIC_BRIGHT = "basic_bright", // bright.pp
        VENUE_POSTPROCESS_BASIC_CONTRAST = "basic_contrast", // film_contrast.pp
        VENUE_POSTPROCESS_BASIC_POSTERIZE = "basic_posterize", // posterize.pp
        VENUE_POSTPROCESS_BASIC_PHOTONEGATIVE = "basic_negative", // photo_negative.pp
        VENUE_POSTPROCESS_BASIC_MIRROR = "basic_mirror", // ProFilm_mirror_a.pp

        // Color filters
        VENUE_POSTPROCESS_COLOR_BLACK_WHITE = "color_b&w", // film_b+w.pp
        VENUE_POSTPROCESS_COLOR_SEPIA = "color_sepia", // film_sepia_ink.pp
        VENUE_POSTPROCESS_COLOR_SILVERTONE = "color_silvertone", // film_silvertone.pp
        VENUE_POSTPROCESS_COLOR_BLACK_WHITE_POLARIZED = "color_polarized_b&w", // contrast_a.pp
        VENUE_POSTPROCESS_COLOR_BLACK_WHITE_CHOPPY = "color_choppy_b&w", // photocopy.pp
        VENUE_POSTPROCESS_COLOR_RED_BLACK = "color_red_black", // horror_movie_special.pp
        VENUE_POSTPROCESS_COLOR_RED_BLUE = "color_red_blue", // ProFilm_psychedelic_blue_red.pp

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
        VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE = "b&w_scanlines", // video_bw.pp
        VENUE_POSTPROCESS_SCANLINES_BLUE = "blue_scanlines", // film_blue_filter.pp
        VENUE_POSTPROCESS_SCANLINES_SECURITY = "security_scanlines", // video_security.pp

        // Trails (video feed delay, a "visual echo")
        VENUE_POSTPROCESS_TRAILS = "trails", // clean_trails.pp
        VENUE_POSTPROCESS_TRAILS_LONG = "long_trails", // video_trails.pp
        VENUE_POSTPROCESS_TRAILS_DESATURATED = "desaturated_trails", // desat_posterize_trails.pp
        VENUE_POSTPROCESS_TRAILS_FLICKER = "flicker_trails", // flicker_trails.pp
        VENUE_POSTPROCESS_TRAILS_SPACEY = "spacey_trails"; // space_woosh.pp
        #endregion

        #region Miscellaneous
        public const string
        VENUE_MISC_BONUS_FX = "bonus_fx",
        VENUE_MISC_FOG_ON = "fog_on",
        VENUE_MISC_FOG_OFF = "fog_off";
        #endregion

        #endregion // Venue
    }
}