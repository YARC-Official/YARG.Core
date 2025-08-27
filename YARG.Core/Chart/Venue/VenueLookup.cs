using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lighting event for the stage of a venue.
    /// </summary>
    public static class VenueLookup
    {
        public enum Type
        {
            Lighting,
            PostProcessing,
            Singalong,
            Spotlight,
            StageEffect,
            CameraCut,
            CameraCutConstraint,

            Unknown = 99
        }
        public static readonly Dictionary<string, (VenueLookup.Type type, string text)> VENUE_TEXT_CONVERSION_LOOKUP = new()
        {
            #region Lighting events
            // Keyframe events
            { "first", (VenueLookup.Type.Lighting, VENUE_LIGHTING_FIRST) },
            { "next",  (VenueLookup.Type.Lighting, VENUE_LIGHTING_NEXT) },
            { "prev",  (VenueLookup.Type.Lighting, VENUE_LIGHTING_PREVIOUS) },

            // RBN1 equivalents for `lighting (chorus)` and `lighting (verse)`
            { "verse",  (VenueLookup.Type.Lighting, VENUE_LIGHTING_VERSE) },
            { "chorus", (VenueLookup.Type.Lighting, VENUE_LIGHTING_CHORUS) },
            #endregion

            #region Post-processing events
            { "bloom.pp",                        (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_BLOOM) },
            { "bright.pp",                       (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_BRIGHT) },
            { "clean_trails.pp",                 (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_TRAILS) },
            { "contrast_a.pp",                   (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE) },
            { "desat_blue.pp",                   (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_DESATURATED_BLUE) },
            { "desat_posterize_trails.pp",       (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_TRAILS_DESATURATED) },
            { "film_contrast.pp",                (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_CONTRAST) },
            { "film_b+w.pp",                     (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_BLACK_WHITE) },
            { "film_sepia_ink.pp",               (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SEPIATONE) },
            { "film_silvertone.pp",              (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SILVERTONE) },
            { "film_contrast_red.pp",            (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_CONTRAST_RED) },
            { "film_contrast_green.pp",          (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_CONTRAST_GREEN) },
            { "film_contrast_blue.pp",           (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_CONTRAST_BLUE) },
            { "film_16mm.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_GRAINY_FILM) },
            { "film_blue_filter.pp",             (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SCANLINES_BLUE) },
            { "flicker_trails.pp",               (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_TRAILS_FLICKERY) },
            { "horror_movie_special.pp",         (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK) },
            { "photocopy.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE) },
            { "photo_negative.pp",               (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_PHOTONEGATIVE) },
            { "posterize.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_POSTERIZE) },
            { "ProFilm_a.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_DEFAULT) },
            { "ProFilm_b.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_DESATURATED_RED) },
            { "ProFilm_mirror_a.pp",             (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_MIRROR) },
            { "ProFilm_psychedelic_blue_red.pp", (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_POLARIZED_RED_BLUE) },
            { "shitty_tv.pp",                    (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION) },
            { "space_woosh.pp",                  (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_TRAILS_SPACEY) },
            { "video_a.pp",                      (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SCANLINES) },
            { "video_bw.pp",                     (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE) },
            { "video_security.pp",               (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_SCANLINES_SECURITY) },
            { "video_trails.pp",                 (VenueLookup.Type.PostProcessing, VENUE_POSTPROCESS_TRAILS_LONG) },
            #endregion

            #region Stage effects
            { "bonusfx",          (VenueLookup.Type.StageEffect, VENUE_STAGE_BONUS_FX) },
            { "bonusfx_optional", (VenueLookup.Type.StageEffect, VENUE_OPTIONAL_EVENT_PREFIX_WITH_SPACE + VENUE_STAGE_BONUS_FX) },
            { "FogOn",            (VenueLookup.Type.StageEffect, VENUE_STAGE_FOG_ON) },
            { "FogOff",           (VenueLookup.Type.StageEffect, VENUE_STAGE_FOG_OFF) },
            #endregion
        };

        public static readonly Dictionary<string, string> VENUE_LIGHTING_CONVERSION_LOOKUP = new()
        {
            #region Keyframed
            // { string.Empty,  VENUE_LIGHTING_DEFAULT }, // Handled by the default case
            { "chorus",      VENUE_LIGHTING_CHORUS },
            { "dischord",    VENUE_LIGHTING_DISCHORD },
            { "manual_cool", VENUE_LIGHTING_COOL_MANUAL },
            { "manual_warm", VENUE_LIGHTING_WARM_MANUAL },
            { "stomp",       VENUE_LIGHTING_STOMP },
            { "verse",       VENUE_LIGHTING_VERSE },
            #endregion

            #region Automatic
            { "blackout_fast",    VENUE_LIGHTING_BLACKOUT_FAST },
            { "blackout_slow",    VENUE_LIGHTING_BLACKOUT_SLOW },
            { "blackout_spot",    VENUE_LIGHTING_BLACKOUT_SPOTLIGHT },
            { "bre",              VENUE_LIGHTING_BIG_ROCK_ENDING },
            { "flare_fast",       VENUE_LIGHTING_FLARE_FAST },
            { "flare_slow",       VENUE_LIGHTING_FLARE_SLOW },
            { "frenzy",           VENUE_LIGHTING_FRENZY },
            { "harmony",          VENUE_LIGHTING_HARMONY },
            { "intro",            VENUE_LIGHTING_INTRO },
            { "loop_cool",        VENUE_LIGHTING_COOL_AUTOMATIC },
            { "loop_warm",        VENUE_LIGHTING_WARM_AUTOMATIC },
            { "searchlights",     VENUE_LIGHTING_SEARCHLIGHTS },
            { "silhouettes",      VENUE_LIGHTING_SILHOUETTES },
            { "silhouettes_spot", VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT },
            { "strobe_fast",      VENUE_LIGHTING_STROBE_FAST },
            { "strobe_slow",      VENUE_LIGHTING_STROBE_SLOW },
            { "sweep",            VENUE_LIGHTING_SWEEP },
            #endregion
        };

        // This is the inverse of everything in the Camera cuts region
        public static readonly Dictionary<string, string> VENUE_DIRECTED_CUT_LOOKUP = new()
        {
            { "directed_guitar", VENUE_CAMERA_DIRECTED_GUITAR },
            { "directed_bass",   VENUE_CAMERA_DIRECTED_BASS },
            { "directed_drums",  VENUE_CAMERA_DIRECTED_DRUMS },
            { "directed_vocals", VENUE_CAMERA_DIRECTED_VOCALS },
            { "directed_stagedive", VENUE_CAMERA_DIRECTED_STAGEDIVE },
            { "directed_crowdsurf", VENUE_CAMERA_DIRECTED_CROWDSURF },
            { "directed_all", VENUE_CAMERA_DIRECTED_ALL },
            { "directed_bre", VENUE_CAMERA_DIRECTED_BRE },
            { "directed_brej", VENUE_CAMERA_DIRECTED_BREJ },
            { "directed_guitar_cam", VENUE_CAMERA_DIRECTED_GUITAR_CAM },
            { "directed_bass_cam",   VENUE_CAMERA_DIRECTED_BASS_CAM },
            { "directed_drums_kd",  VENUE_CAMERA_DIRECTED_DRUMS_KD },
            { "directed_drums_lt",  VENUE_CAMERA_DIRECTED_DRUMS_LT },
            { "directed_drums_np",  VENUE_CAMERA_DIRECTED_DRUMS_NP },
            { "directed_crowd_g",   VENUE_CAMERA_DIRECTED_CROWD_G },
            { "directed_crowd_b",   VENUE_CAMERA_DIRECTED_CROWD_B },
            { "directed_crowd_pnt", VENUE_CAMERA_DIRECTED_CROWD_PNT },
            { "directed_duo_drums", VENUE_CAMERA_DIRECTED_DUO_DRUMS },
            { "directed_guitar_cls", VENUE_CAMERA_DIRECTED_GUITAR_CLS },
            { "directed_bass_cls",   VENUE_CAMERA_DIRECTED_BASS_CLS },
            { "directed_vocals_cam", VENUE_CAMERA_DIRECTED_VOCALS_CAM },
            { "directed_vocals_cls", VENUE_CAMERA_DIRECTED_VOCALS_CLS },
            { "directed_all_cam", VENUE_CAMERA_DIRECTED_ALL_CAM },
            { "directed_all_lt", VENUE_CAMERA_DIRECTED_ALL_LT },
            { "directed_all_yeah", VENUE_CAMERA_DIRECTED_ALL_YEAH },
            { "default", VENUE_CAMERA_DEFAULT },
            { "directed_guitar_np", VENUE_CAMERA_DIRECTED_GUITAR_NP },
            { "directed_bass_np",   VENUE_CAMERA_DIRECTED_BASS_NP },
            { "directed_vocals_np", VENUE_CAMERA_DIRECTED_VOCALS_NP },
            { "directed_drums_pnt",  VENUE_CAMERA_DIRECTED_CROWD_PNT },
        };

        public static readonly Dictionary<string, string> VENUE_CAMERA_CUT_LOOKUP = new()
        {
            // Four shots
            { "all_behind", VENUE_CAMERA_ALL_BEHIND },
            { "all_far", VENUE_CAMERA_ALL_FAR },
            { "all_near", VENUE_CAMERA_ALL_NEAR },

            // Three shots (no drums)
            { "front_behind", VENUE_CAMERA_FRONT_BEHIND },
            { "front_near", VENUE_CAMERA_FRONT_NEAR },

            // One character standard shots
            { "d_behind", VENUE_CAMERA_DRUMS_BEHIND },
            { "d_near", VENUE_CAMERA_DRUMS_NEAR },
            { "v_behind", VENUE_CAMERA_VOCALS_BEHIND },
            { "v_near", VENUE_CAMERA_VOCALS_NEAR },
            { "b_behind", VENUE_CAMERA_BASS_BEHIND },
            { "b_near", VENUE_CAMERA_BASS_NEAR },
            { "g_behind", VENUE_CAMERA_GUITAR_BEHIND },
            { "g_near", VENUE_CAMERA_GUITAR_NEAR },
            { "k_behind", VENUE_CAMERA_KEYS_BEHIND },
            { "k_near", VENUE_CAMERA_KEYS_NEAR },

            // One character closeups
            { "d_closeup_hand", VENUE_CAMERA_DRUMS_CLOSEUP_HAND },
            { "d_closeup_head", VENUE_CAMERA_DRUMS_CLOSEUP_HEAD },
            { "v_closeup", VENUE_CAMERA_VOCALS_CLOSEUP },
            { "b_closeup_hand", VENUE_CAMERA_BASS_CLOSEUP_HAND },
            { "b_closeup_head", VENUE_CAMERA_BASS_CLOSEUP_HEAD },
            { "g_closeup_hand", VENUE_CAMERA_GUITAR_CLOSEUP_HAND },
            { "g_closeup_head", VENUE_CAMERA_GUITAR_CLOSEUP_HEAD },
            { "k_closeup_hand", VENUE_CAMERA_KEYS_CLOSEUP_HAND },
            { "k_closeup_head", VENUE_CAMERA_KEYS_CLOSEUP_HEAD },

            // Two character shots
            { "dv_near", VENUE_CAMERA_DRUMS_VOCALS_NEAR },
            { "bd_near", VENUE_CAMERA_BASS_DRUMS_NEAR },
            { "dg_near", VENUE_CAMERA_DRUMS_GUITAR_NEAR },
            { "bv_behind", VENUE_CAMERA_BASS_VOCALS_BEHIND },
            { "bv_near", VENUE_CAMERA_BASS_VOCALS_NEAR },
            { "gv_behind", VENUE_CAMERA_GUITAR_VOCALS_BEHIND },
            { "gv_near", VENUE_CAMERA_GUITAR_VOCALS_NEAR },
            { "kv_behind", VENUE_CAMERA_KEYS_VOCALS_BEHIND },
            { "kv_near", VENUE_CAMERA_KEYS_VOCALS_NEAR },
            { "bg_behind", VENUE_CAMERA_BASS_GUITAR_BEHIND },
            { "bg_near", VENUE_CAMERA_BASS_GUITAR_NEAR },
            { "bk_behind", VENUE_CAMERA_BASS_KEYS_BEHIND },
            { "bk_near", VENUE_CAMERA_BASS_KEYS_NEAR },
            { "gk_behind", VENUE_CAMERA_GUITAR_KEYS_BEHIND },
            { "gk_near", VENUE_CAMERA_GUITAR_KEYS_NEAR }
        };

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

        #region Camera cuts

        public const string
            // Guitarist
            VENUE_CAMERA_DIRECTED_GUITAR     = "directed_guitar",
            VENUE_CAMERA_DIRECTED_GUITAR_CAM = "directed_guitar_cam",
            VENUE_CAMERA_DIRECTED_GUITAR_CLS = "directed_guitar_cls",
            VENUE_CAMERA_DIRECTED_GUITAR_NP  = "directed_guitar_np",
            VENUE_CAMERA_DIRECTED_CROWD_G    = "directed_crowd_g",
            // Bassist
            VENUE_CAMERA_DIRECTED_BASS     = "directed_bass",
            VENUE_CAMERA_DIRECTED_BASS_CAM = "directed_bass_cam",
            VENUE_CAMERA_DIRECTED_BASS_CLS = "directed_bass_cls",
            VENUE_CAMERA_DIRECTED_BASS_NP  = "directed_bass_np",
            VENUE_CAMERA_DIRECTED_CROWD_B  = "directed_crowd_b",
            // Drummer
            VENUE_CAMERA_DIRECTED_DRUMS     = "directed_drums",
            VENUE_CAMERA_DIRECTED_DRUMS_KD  = "directed_drums_kd",
            VENUE_CAMERA_DIRECTED_DRUMS_LT  = "directed_drums_lt",
            VENUE_CAMERA_DIRECTED_DRUMS_NP  = "directed_drums_np",
            VENUE_CAMERA_DIRECTED_CROWD_PNT = "directed_drums_pnt",
            VENUE_CAMERA_DIRECTED_DUO_DRUMS = "directed_duo_drums",
            // Vocalist
            VENUE_CAMERA_DIRECTED_VOCALS     = "directed_vocals",
            VENUE_CAMERA_DIRECTED_VOCALS_CAM = "directed_vocals_cam",
            VENUE_CAMERA_DIRECTED_VOCALS_CLS = "directed_vocals_cls",
            VENUE_CAMERA_DIRECTED_VOCALS_NP  = "directed_vocals_np",
            VENUE_CAMERA_DIRECTED_STAGEDIVE  = "directed_stagedive",
            VENUE_CAMERA_DIRECTED_CROWDSURF  = "directed_crowdsurf",
            // Full Band
            VENUE_CAMERA_DIRECTED_ALL      = "directed_all",
            VENUE_CAMERA_DIRECTED_ALL_CAM  = "directed_all_cam",
            VENUE_CAMERA_DIRECTED_ALL_LT   = "directed_all_lt",
            VENUE_CAMERA_DIRECTED_ALL_YEAH = "directed_all_yeah",
            VENUE_CAMERA_DIRECTED_BRE      = "directed_bre",
            VENUE_CAMERA_DIRECTED_BREJ     = "directed_brej",
            VENUE_CAMERA_DEFAULT           = "default",
            VENUE_CAMERA_RANDOM            = "random";

        #endregion // Camera cuts

        #region RBN2 Camera Cuts

        public const string
            // Generic four shots
            VENUE_CAMERA_ALL_BEHIND = "all_behind",
            VENUE_CAMERA_ALL_FAR    = "all_far",
            VENUE_CAMERA_ALL_NEAR   = "all_near",

            // Three shots (no drums)
            VENUE_CAMERA_FRONT_BEHIND = "front_behind",
            VENUE_CAMERA_FRONT_NEAR   = "front_near",

            // Single character standard shots
            VENUE_CAMERA_DRUMS_BEHIND  = "d_behind",
            VENUE_CAMERA_DRUMS_NEAR    = "d_near",
            VENUE_CAMERA_VOCALS_BEHIND = "v_behind",
            VENUE_CAMERA_VOCALS_NEAR   = "v_near",
            VENUE_CAMERA_BASS_BEHIND   = "b_behind",
            VENUE_CAMERA_BASS_NEAR     = "b_near",
            VENUE_CAMERA_GUITAR_BEHIND = "g_behind",
            VENUE_CAMERA_GUITAR_NEAR   = "g_near",
            VENUE_CAMERA_KEYS_BEHIND   = "k_behind",
            VENUE_CAMERA_KEYS_NEAR     = "k_near",

            // Single character closeups
            VENUE_CAMERA_DRUMS_CLOSEUP_HAND  = "d_closeup_hand",
            VENUE_CAMERA_DRUMS_CLOSEUP_HEAD  = "d_closeup_head",
            VENUE_CAMERA_VOCALS_CLOSEUP      = "v_closeup",
            VENUE_CAMERA_BASS_CLOSEUP_HAND   = "b_closeup_hand",
            VENUE_CAMERA_BASS_CLOSEUP_HEAD   = "b_closeup_head",
            VENUE_CAMERA_GUITAR_CLOSEUP_HAND = "g_closeup_hand",
            VENUE_CAMERA_GUITAR_CLOSEUP_HEAD = "g_closeup_head",
            VENUE_CAMERA_KEYS_CLOSEUP_HAND   = "k_closeup_hand",
            VENUE_CAMERA_KEYS_CLOSEUP_HEAD   = "k_closeup_head",

            // Two character shots
            VENUE_CAMERA_DRUMS_VOCALS_NEAR    = "dv_near",
            VENUE_CAMERA_BASS_DRUMS_NEAR      = "bd_near",
            VENUE_CAMERA_DRUMS_GUITAR_NEAR    = "dg_near",
            VENUE_CAMERA_BASS_VOCALS_BEHIND   = "bv_behind",
            VENUE_CAMERA_BASS_VOCALS_NEAR     = "bv_near",
            VENUE_CAMERA_GUITAR_VOCALS_BEHIND = "gv_behind",
            VENUE_CAMERA_GUITAR_VOCALS_NEAR   = "gv_near",
            VENUE_CAMERA_KEYS_VOCALS_BEHIND   = "kv_behind",
            VENUE_CAMERA_KEYS_VOCALS_NEAR     = "kv_near",
            VENUE_CAMERA_BASS_GUITAR_BEHIND   = "bg_behind",
            VENUE_CAMERA_BASS_GUITAR_NEAR     = "bg_near",
            VENUE_CAMERA_BASS_KEYS_BEHIND     = "bk_behind",
            VENUE_CAMERA_BASS_KEYS_NEAR       = "bk_near",
            VENUE_CAMERA_GUITAR_KEYS_BEHIND   = "gk_behind",
            VENUE_CAMERA_GUITAR_KEYS_NEAR     = "gk_near";

        #endregion // RBN2 Camera Cuts

        #region Camera cut constraints

        public const string
            VENUE_CAMERA_CONSTRAINT_NO_BEHIND  = "no_behind",
            VENUE_CAMERA_CONSTRAINT_ONLY_FAR   = "only_far",
            VENUE_CAMERA_CONSTRAINT_NO_CLOSE  = "no_close",
            VENUE_CAMERA_CONSTRAINT_ONLY_CLOSE = "only_close";
        #endregion // Camera cut constraints

        #endregion // Venue

        #region Lookups

        // TODO: Make more subjects and assign them to the appropriate cuts
        public static readonly Dictionary<string, CameraCutEvent.CameraCutSubject> CameraCutSubjectLookup = new()
        {
            { VENUE_CAMERA_DIRECTED_ALL, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_ALL_CAM, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_ALL_LT, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_ALL_YEAH, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_BRE, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_BREJ, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_GUITAR, CameraCutEvent.CameraCutSubject.Guitar },
            { VENUE_CAMERA_DIRECTED_GUITAR_CAM, CameraCutEvent.CameraCutSubject.Guitar },
            { VENUE_CAMERA_DIRECTED_GUITAR_CLS, CameraCutEvent.CameraCutSubject.GuitarCloseup },
            { VENUE_CAMERA_DIRECTED_GUITAR_NP, CameraCutEvent.CameraCutSubject.Guitar },
            { VENUE_CAMERA_DIRECTED_CROWD_G, CameraCutEvent.CameraCutSubject.Stage},
            { VENUE_CAMERA_DIRECTED_BASS, CameraCutEvent.CameraCutSubject.Bass },
            { VENUE_CAMERA_DIRECTED_BASS_CAM, CameraCutEvent.CameraCutSubject.Bass },
            { VENUE_CAMERA_DIRECTED_BASS_CLS, CameraCutEvent.CameraCutSubject.BassCloseup },
            { VENUE_CAMERA_DIRECTED_BASS_NP, CameraCutEvent.CameraCutSubject.Bass },
            { VENUE_CAMERA_DIRECTED_CROWD_B, CameraCutEvent.CameraCutSubject.Bass },
            { VENUE_CAMERA_DIRECTED_DRUMS, CameraCutEvent.CameraCutSubject.Drums },
            { VENUE_CAMERA_DIRECTED_DRUMS_KD, CameraCutEvent.CameraCutSubject.DrumsKick },
            { VENUE_CAMERA_DIRECTED_DRUMS_LT, CameraCutEvent.CameraCutSubject.Drums },
            { VENUE_CAMERA_DIRECTED_DRUMS_NP, CameraCutEvent.CameraCutSubject.Drums },
            { VENUE_CAMERA_DIRECTED_DUO_DRUMS, CameraCutEvent.CameraCutSubject.Drums },
            { VENUE_CAMERA_DIRECTED_CROWD_PNT, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_STAGEDIVE, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_CROWDSURF, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_DIRECTED_VOCALS, CameraCutEvent.CameraCutSubject.Vocals },
            { VENUE_CAMERA_DIRECTED_VOCALS_CAM, CameraCutEvent.CameraCutSubject.Vocals },
            { VENUE_CAMERA_DIRECTED_VOCALS_CLS, CameraCutEvent.CameraCutSubject.Vocals },
            { VENUE_CAMERA_DIRECTED_VOCALS_NP, CameraCutEvent.CameraCutSubject.Vocals },
            { VENUE_CAMERA_DEFAULT, CameraCutEvent.CameraCutSubject.Stage },
            { VENUE_CAMERA_RANDOM, CameraCutEvent.CameraCutSubject.Random },

            // RBN2 text events
            { VENUE_CAMERA_ALL_BEHIND, CameraCutEvent.CameraCutSubject.AllBehind },
            { VENUE_CAMERA_ALL_FAR, CameraCutEvent.CameraCutSubject.AllFar },
            { VENUE_CAMERA_ALL_NEAR, CameraCutEvent.CameraCutSubject.AllNear },
            { VENUE_CAMERA_FRONT_BEHIND, CameraCutEvent.CameraCutSubject.BehindNoDrum },
            { VENUE_CAMERA_FRONT_NEAR, CameraCutEvent.CameraCutSubject.NearNoDrum },
            { VENUE_CAMERA_DRUMS_BEHIND, CameraCutEvent.CameraCutSubject.DrumsBehind },
            { VENUE_CAMERA_DRUMS_NEAR, CameraCutEvent.CameraCutSubject.Drums },
            { VENUE_CAMERA_VOCALS_BEHIND, CameraCutEvent.CameraCutSubject.VocalsBehind },
            { VENUE_CAMERA_VOCALS_NEAR, CameraCutEvent.CameraCutSubject.Vocals },
            { VENUE_CAMERA_BASS_BEHIND, CameraCutEvent.CameraCutSubject.BassBehind },
            { VENUE_CAMERA_BASS_NEAR, CameraCutEvent.CameraCutSubject.Bass },
            { VENUE_CAMERA_GUITAR_BEHIND, CameraCutEvent.CameraCutSubject.GuitarBehind },
            { VENUE_CAMERA_GUITAR_NEAR, CameraCutEvent.CameraCutSubject.Guitar },
            { VENUE_CAMERA_KEYS_BEHIND, CameraCutEvent.CameraCutSubject.KeysBehind },
            { VENUE_CAMERA_KEYS_NEAR, CameraCutEvent.CameraCutSubject.Keys },
            { VENUE_CAMERA_DRUMS_CLOSEUP_HAND, CameraCutEvent.CameraCutSubject.DrumsCloseupHand },
            { VENUE_CAMERA_DRUMS_CLOSEUP_HEAD, CameraCutEvent.CameraCutSubject.DrumsCloseupHead },
            { VENUE_CAMERA_VOCALS_CLOSEUP, CameraCutEvent.CameraCutSubject.VocalsCloseup },
            { VENUE_CAMERA_BASS_CLOSEUP_HAND, CameraCutEvent.CameraCutSubject.BassCloseup },
            { VENUE_CAMERA_BASS_CLOSEUP_HEAD, CameraCutEvent.CameraCutSubject.BassCloseupHead },
            { VENUE_CAMERA_GUITAR_CLOSEUP_HAND, CameraCutEvent.CameraCutSubject.GuitarCloseup },
            { VENUE_CAMERA_GUITAR_CLOSEUP_HEAD, CameraCutEvent.CameraCutSubject.GuitarCloseupHead },
            { VENUE_CAMERA_KEYS_CLOSEUP_HAND, CameraCutEvent.CameraCutSubject.KeysCloseupHand },
            { VENUE_CAMERA_KEYS_CLOSEUP_HEAD, CameraCutEvent.CameraCutSubject.KeysCloseupHead },
            { VENUE_CAMERA_DRUMS_VOCALS_NEAR, CameraCutEvent.CameraCutSubject.DrumsVocals },
            { VENUE_CAMERA_BASS_DRUMS_NEAR, CameraCutEvent.CameraCutSubject.BassDrums },
            { VENUE_CAMERA_DRUMS_GUITAR_NEAR, CameraCutEvent.CameraCutSubject.DrumsGuitar },
            { VENUE_CAMERA_BASS_VOCALS_BEHIND, CameraCutEvent.CameraCutSubject.BassVocalsBehind },
            { VENUE_CAMERA_BASS_VOCALS_NEAR, CameraCutEvent.CameraCutSubject.BassVocals },
            { VENUE_CAMERA_GUITAR_VOCALS_BEHIND, CameraCutEvent.CameraCutSubject.GuitarVocalsBehind },
            { VENUE_CAMERA_GUITAR_VOCALS_NEAR, CameraCutEvent.CameraCutSubject.GuitarVocals },
            { VENUE_CAMERA_KEYS_VOCALS_BEHIND, CameraCutEvent.CameraCutSubject.KeysVocalsBehind },
            { VENUE_CAMERA_KEYS_VOCALS_NEAR, CameraCutEvent.CameraCutSubject.KeysVocals },
            { VENUE_CAMERA_BASS_GUITAR_BEHIND, CameraCutEvent.CameraCutSubject.BassGuitarBehind },
            { VENUE_CAMERA_BASS_GUITAR_NEAR, CameraCutEvent.CameraCutSubject.BassGuitar },
            { VENUE_CAMERA_BASS_KEYS_BEHIND, CameraCutEvent.CameraCutSubject.BassKeysBehind },
            { VENUE_CAMERA_BASS_KEYS_NEAR, CameraCutEvent.CameraCutSubject.BassKeys },
            { VENUE_CAMERA_GUITAR_KEYS_BEHIND, CameraCutEvent.CameraCutSubject.GuitarKeysBehind },
            { VENUE_CAMERA_GUITAR_KEYS_NEAR, CameraCutEvent.CameraCutSubject.GuitarKeys }
        };

        public static readonly Dictionary<string, CameraCutEvent.CameraCutConstraint> CameraCutConstraintLookup = new()
        {
            { VENUE_CAMERA_CONSTRAINT_NO_BEHIND, CameraCutEvent.CameraCutConstraint.NoBehind },
            { VENUE_CAMERA_CONSTRAINT_ONLY_FAR, CameraCutEvent.CameraCutConstraint.OnlyFar },
            { VENUE_CAMERA_CONSTRAINT_NO_CLOSE, CameraCutEvent.CameraCutConstraint.NoClose },
            { VENUE_CAMERA_CONSTRAINT_ONLY_CLOSE, CameraCutEvent.CameraCutConstraint.OnlyClose }
        };

        public static readonly Dictionary<string, VenueEventFlags> FlagPrefixLookup = new()
        {
            { VENUE_OPTIONAL_EVENT_PREFIX, VenueEventFlags.Optional },
        };

        public static readonly Dictionary<string, Performer> PerformerLookup = new()
        {
            { VENUE_PERFORMER_GUITAR, Performer.Guitar },
            { VENUE_PERFORMER_BASS,   Performer.Bass },
            { VENUE_PERFORMER_DRUMS,  Performer.Drums },
            { VENUE_PERFORMER_VOCALS, Performer.Vocals },
            { VENUE_PERFORMER_KEYS,   Performer.Keyboard },
        };

        public static readonly Dictionary<string, LightingType> LightingLookup = new()
        {
            // Keyframed
            { VENUE_LIGHTING_DEFAULT,     LightingType.Default },
            { VENUE_LIGHTING_DISCHORD,    LightingType.Dischord },
            { VENUE_LIGHTING_CHORUS,      LightingType.Chorus },
            { VENUE_LIGHTING_COOL_MANUAL, LightingType.CoolManual },
            { VENUE_LIGHTING_STOMP,       LightingType.Stomp },
            { VENUE_LIGHTING_VERSE,       LightingType.Verse },
            { VENUE_LIGHTING_WARM_MANUAL, LightingType.WarmManual },

            // Automatic
            { VENUE_LIGHTING_BIG_ROCK_ENDING,        LightingType.BigRockEnding },
            { VENUE_LIGHTING_BLACKOUT_FAST,          LightingType.BlackoutFast },
            { VENUE_LIGHTING_BLACKOUT_SLOW,          LightingType.BlackoutSlow },
            { VENUE_LIGHTING_BLACKOUT_SPOTLIGHT,     LightingType.BlackoutSpotlight },
            { VENUE_LIGHTING_COOL_AUTOMATIC,         LightingType.CoolAutomatic },
            { VENUE_LIGHTING_FLARE_FAST,             LightingType.FlareFast },
            { VENUE_LIGHTING_FLARE_SLOW,             LightingType.FlareSlow },
            { VENUE_LIGHTING_FRENZY,                 LightingType.Frenzy },
            { VENUE_LIGHTING_INTRO,                  LightingType.Intro },
            { VENUE_LIGHTING_HARMONY,                LightingType.Harmony },
            { VENUE_LIGHTING_SILHOUETTES,            LightingType.Silhouettes },
            { VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT,  LightingType.SilhouettesSpotlight },
            { VENUE_LIGHTING_SEARCHLIGHTS,           LightingType.Searchlights },
            { VENUE_LIGHTING_STROBE_FAST,            LightingType.StrobeFast },
            { VENUE_LIGHTING_STROBE_SLOW,            LightingType.StrobeSlow },
            { VENUE_LIGHTING_SWEEP,                  LightingType.Sweep },
            { VENUE_LIGHTING_WARM_AUTOMATIC,         LightingType.WarmAutomatic },

            // Keyframes
            { VENUE_LIGHTING_FIRST,    LightingType.KeyframeFirst },
            { VENUE_LIGHTING_NEXT,     LightingType.KeyframeNext },
            { VENUE_LIGHTING_PREVIOUS, LightingType.KeyframePrevious },
        };

        public static readonly Dictionary<string, PostProcessingType> PostProcessLookup = new()
        {
            { VENUE_POSTPROCESS_DEFAULT, PostProcessingType.Default },

            // Basic effects
            { VENUE_POSTPROCESS_BLOOM,         PostProcessingType.Bloom },
            { VENUE_POSTPROCESS_BRIGHT,        PostProcessingType.Bright },
            { VENUE_POSTPROCESS_CONTRAST,      PostProcessingType.Contrast },
            { VENUE_POSTPROCESS_MIRROR,        PostProcessingType.Mirror },
            { VENUE_POSTPROCESS_PHOTONEGATIVE, PostProcessingType.PhotoNegative },
            { VENUE_POSTPROCESS_POSTERIZE,     PostProcessingType.Posterize },

            // Color filters/effects
            { VENUE_POSTPROCESS_BLACK_WHITE,             PostProcessingType.BlackAndWhite },
            { VENUE_POSTPROCESS_SEPIATONE,               PostProcessingType.SepiaTone },
            { VENUE_POSTPROCESS_SILVERTONE,              PostProcessingType.SilverTone },
            { VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE,      PostProcessingType.Choppy_BlackAndWhite },
            { VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK, PostProcessingType.PhotoNegative_RedAndBlack },
            { VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE,   PostProcessingType.Polarized_BlackAndWhite },
            { VENUE_POSTPROCESS_POLARIZED_RED_BLUE,      PostProcessingType.Polarized_RedAndBlue },
            { VENUE_POSTPROCESS_DESATURATED_RED,         PostProcessingType.Desaturated_Red },
            { VENUE_POSTPROCESS_DESATURATED_BLUE,        PostProcessingType.Desaturated_Blue },
            { VENUE_POSTPROCESS_CONTRAST_RED,            PostProcessingType.Contrast_Red },
            { VENUE_POSTPROCESS_CONTRAST_GREEN,          PostProcessingType.Contrast_Green },
            { VENUE_POSTPROCESS_CONTRAST_BLUE,           PostProcessingType.Contrast_Blue },

            // Grainy
            { VENUE_POSTPROCESS_GRAINY_FILM,                 PostProcessingType.Grainy_Film },
            { VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION, PostProcessingType.Grainy_ChromaticAbberation },

            // Scanlines
            { VENUE_POSTPROCESS_SCANLINES,             PostProcessingType.Scanlines },
            { VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE, PostProcessingType.Scanlines_BlackAndWhite },
            { VENUE_POSTPROCESS_SCANLINES_BLUE,        PostProcessingType.Scanlines_Blue },
            { VENUE_POSTPROCESS_SCANLINES_SECURITY,    PostProcessingType.Scanlines_Security },

            // Trails
            { VENUE_POSTPROCESS_TRAILS,             PostProcessingType.Trails },
            { VENUE_POSTPROCESS_TRAILS_LONG,        PostProcessingType.Trails_Long },
            { VENUE_POSTPROCESS_TRAILS_DESATURATED, PostProcessingType.Trails_Desaturated },
            { VENUE_POSTPROCESS_TRAILS_FLICKERY,    PostProcessingType.Trails_Flickery },
            { VENUE_POSTPROCESS_TRAILS_SPACEY,      PostProcessingType.Trails_Spacey },
        };

        public static readonly Dictionary<string, StageEffect> StageEffectLookup = new()
        {
            { VENUE_STAGE_BONUS_FX, StageEffect.BonusFx },
            { VENUE_STAGE_FOG_ON,   StageEffect.FogOn },
            { VENUE_STAGE_FOG_OFF,  StageEffect.FogOff },
        };
        #endregion
    }
}