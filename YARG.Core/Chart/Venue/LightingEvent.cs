using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lighting event for the stage of a venue.
    /// </summary>
    public class LightingEvent : VenueEvent, ICloneable<LightingEvent>
    {
        public LightingType Type { get; }

        public LightingEvent(LightingType type, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public LightingEvent(LightingEvent other) : base(other)
        {
            Type = other.Type;
        }

        public LightingEvent Clone()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible lighting types.
    /// </summary>
    public enum LightingType
    {
        // Keyframed
        Default,
        Dischord,
        Chorus,
        Cool_Manual,
        Stomp,
        Verse,
        Warm_Manual,

        // Automatic
        BigRockEnding,
        BlackoutFast,
        BlackoutSlow,
        BlackoutSpotlight,
        CoolAutomatic,
        FlareFast,
        FlareSlow,
        Frenzy,
        Intro,
        Harmony,
        Silhouettes,
        SilhouettesSpotlight,
        Searchlights,
        StrobeFastest,
        StrobeFast,
        StrobeMedium,
        StrobeSlow,
        StrobeOff,
        Sweep,
        Warm_Automatic,

        // Keyframe events
        KeyframeFirst,
        KeyframeNext,
        KeyframePrevious,

        //YARG internal
        Menu,
        Score,
        NoCue,
    }
}
