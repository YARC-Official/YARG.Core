using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Utility;

using MoonVenueEvent = MoonscraperChartEditor.Song.VenueEvent;

namespace YARG.Core.Chart
{
    using static TextEventDefinitions;

    internal partial class MoonSongLoader : ISongLoader
    {
        #region Lookups
        private static readonly Dictionary<string, VenueEventFlags> FlagPrefixLookup = new()
        {
            { VENUE_OPTIONAL_EVENT_PREFIX, VenueEventFlags.Optional },
        };

        private static readonly Dictionary<string, Performer> PerformerLookup = new()
        {
            { VENUE_PERFORMER_GUITAR, Performer.Guitar },
            { VENUE_PERFORMER_BASS,   Performer.Bass },
            { VENUE_PERFORMER_DRUMS,  Performer.Drums },
            { VENUE_PERFORMER_VOCALS, Performer.Vocals },
            { VENUE_PERFORMER_KEYS,   Performer.Keyboard },
        };

        private static readonly Dictionary<string, LightingType> LightingLookup = new()
        {
            // Keyframed
            { VENUE_LIGHTING_DEFAULT,     LightingType.Default },
            { VENUE_LIGHTING_DISCHORD,    LightingType.Dischord },
            { VENUE_LIGHTING_CHORUS,      LightingType.Chorus },
            { VENUE_LIGHTING_COOL_MANUAL, LightingType.Cool_Manual },
            { VENUE_LIGHTING_STOMP,       LightingType.Stomp },
            { VENUE_LIGHTING_VERSE,       LightingType.Verse },
            { VENUE_LIGHTING_WARM_MANUAL, LightingType.Warm_Manual },

            // Automatic
            { VENUE_LIGHTING_BIG_ROCK_ENDING,        LightingType.BigRockEnding },
            { VENUE_LIGHTING_BLACKOUT_FAST,          LightingType.Blackout_Fast },
            { VENUE_LIGHTING_BLACKOUT_SLOW,          LightingType.Blackout_Slow },
            { VENUE_LIGHTING_BLACKOUT_SPOTLIGHT,     LightingType.Blackout_Spotlight },
            { VENUE_LIGHTING_COOL_AUTOMATIC,         LightingType.Cool_Automatic },
            { VENUE_LIGHTING_FLARE_FAST,             LightingType.Flare_Fast },
            { VENUE_LIGHTING_FLARE_SLOW,             LightingType.Flare_Slow },
            { VENUE_LIGHTING_FRENZY,                 LightingType.Frenzy },
            { VENUE_LIGHTING_INTRO,                  LightingType.Intro },
            { VENUE_LIGHTING_HARMONY,                LightingType.Harmony },
            { VENUE_LIGHTING_SILHOUETTES,            LightingType.Silhouettes },
            { VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT,  LightingType.Silhouettes_Spotlight },
            { VENUE_LIGHTING_SEARCHLIGHTS,           LightingType.Searchlights },
            { VENUE_LIGHTING_STROBE_FAST,            LightingType.Strobe_Fast },
            { VENUE_LIGHTING_STROBE_SLOW,            LightingType.Strobe_Slow },
            { VENUE_LIGHTING_SWEEP,                  LightingType.Sweep },
            { VENUE_LIGHTING_WARM_AUTOMATIC,         LightingType.Warm_Automatic },

            // Keyframes
            { VENUE_LIGHTING_FIRST,    LightingType.Keyframe_First },
            { VENUE_LIGHTING_NEXT,     LightingType.Keyframe_Next },
            { VENUE_LIGHTING_PREVIOUS, LightingType.Keyframe_Previous },
        };

        private static readonly Dictionary<string, PostProcessingType> PostProcessLookup = new()
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

        private static readonly Dictionary<string, StageEffect> StageEffectLookup = new()
        {
            { VENUE_STAGE_BONUS_FX, StageEffect.BonusFx },
            { VENUE_STAGE_FOG_ON,   StageEffect.FogOn },
            { VENUE_STAGE_FOG_OFF,  StageEffect.FogOff },
        };
        #endregion

        public VenueTrack LoadVenueTrack()
        {
            var lightingEvents = new List<LightingEvent>();
            var postProcessingEvents = new List<PostProcessingEvent>();
            var performerEvents = new List<PerformerEvent>();
            var stageEvents = new List<StageEffectEvent>();

            // For merging spotlights/singalongs into a single event
            MoonVenueEvent? spotlightCurrentEvent = null;
            MoonVenueEvent? singalongCurrentEvent = null;
            var spotlightPerformers = Performer.None;
            var singalongPerformers = Performer.None;

            foreach (var moonVenue in _moonSong.venue)
            {
                // Prefix flags
                var splitter = moonVenue.title.AsSpan().Split(' ');
                splitter.MoveNext();
                var flags = VenueEventFlags.None;
                foreach (var (prefix, flag) in FlagPrefixLookup)
                {
                    if (splitter.Current.Equals(prefix, StringComparison.Ordinal))
                    {
                        flags |= flag;
                        splitter.MoveNext();
                    }
                }

                // Taking the allocation L here, the only way to access with a span is by going over
                // all the key-value pairs, which is 5x slower at even just 25 elements (O(n) vs O(1) with a string)
                // There's a lot of other allocations happening here anyways lol
                string text = splitter.CurrentToEnd.ToString();
                switch (moonVenue.type)
                {
                    case MoonVenueEvent.Type.Lighting:
                    {
                        if (!LightingLookup.TryGetValue(text, out var type))
                            continue;
                        lightingEvents.Add(new(type, moonVenue.time, moonVenue.tick));
                        break;
                    }

                    case MoonVenueEvent.Type.PostProcessing:
                    {
                        if (!PostProcessLookup.TryGetValue(text, out var type))
                            continue;
                        postProcessingEvents.Add(new(type, moonVenue.time, moonVenue.tick));
                        break;
                    }

                    case MoonVenueEvent.Type.Singalong:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Singalong, moonVenue,
                            ref singalongCurrentEvent, ref singalongPerformers);
                        break;
                    }

                    case MoonVenueEvent.Type.Spotlight:
                    {
                        HandlePerformerEvent(performerEvents, PerformerEventType.Spotlight, moonVenue,
                            ref spotlightCurrentEvent, ref spotlightPerformers);
                        break;
                    }

                    case MoonVenueEvent.Type.StageEffect:
                    {
                        if (!StageEffectLookup.TryGetValue(text, out var type))
                            continue;
                        stageEvents.Add(new(type, flags, moonVenue.time, moonVenue.tick));
                        break;
                    }

                    default:
                    {
                        YargTrace.DebugWarning($"Unrecognized venue text event '{text}'!");
                        continue;
                    }
                }
            }

            lightingEvents.TrimExcess();
            postProcessingEvents.TrimExcess();
            performerEvents.TrimExcess();
            stageEvents.TrimExcess();

            return new(lightingEvents, postProcessingEvents, performerEvents, stageEvents);
        }

        private void HandlePerformerEvent(List<PerformerEvent> events, PerformerEventType type, MoonVenueEvent moonEvent,
            ref MoonVenueEvent? currentEvent, ref Performer performers)
        {
            // First event
            if (currentEvent == null)
            {
                currentEvent = moonEvent;
            }
            // Start of a new event
            else if (currentEvent.tick != moonEvent.tick && performers != Performer.None)
            {
                // Add tracked event
                events.Add(new(type, performers, currentEvent.time, GetLengthInTime(currentEvent),
                    currentEvent.tick, currentEvent.length));

                // Track new event
                currentEvent = moonEvent;
                performers = Performer.None;
            }

            // Sing-along events are not optional, use the text directly
            if (!PerformerLookup.TryGetValue(moonEvent.title, out var performer))
                return;
            performers |= performer;
        }

        private double GetLengthInTime(MoonVenueEvent ev)
        {
            return GetLengthInTime(ev.time, ev.tick, ev.length);
        }
    }
}