namespace YARG.Core.Chart
{
    /// <summary>
    /// A lighting event for the stage of a venue.
    /// </summary>
    public class LightingEvent : VenueEvent
    {
        public LightingType Type { get; }

        public LightingEvent(LightingType type, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Type = type;
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
        Blackout_Fast,
        Blackout_Slow,
        Blackout_Spotlight,
        Cool_Automatic,
        Flare_Fast,
        Flare_Slow,
        Frenzy,
        Intro,
        Harmony,
        Silhouettes,
        Silhouettes_Spotlight,
        Searchlights,
        Strobe_Fast,
        Strobe_Slow,
        Sweep,
        Warm_Automatic,

        // Keyframe events
        Keyframe_First,
        Keyframe_Next,
        Keyframe_Previous,
    }
}