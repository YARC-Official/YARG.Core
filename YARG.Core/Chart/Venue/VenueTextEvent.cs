namespace YARG.Core.Chart
{
    /// <summary>
    /// A venue text event, for more generic/miscellaneous actions.
    /// </summary>
    public class VenueTextEvent : VenueEvent
    {
        public string Text { get; }

        public VenueTextEvent(string text, VenueEventFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Definitions for miscellaneous venue events.
    /// </summary>
    public static class VenueMiscellaneous
    {
        public const string
        BONUS_FX = "bonus_fx",
        FOG_ON = "fog_on",
        FOG_OFF = "fog_off";
    }
}