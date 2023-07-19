namespace YARG.Core.Chart
{
    /// <summary>
    /// A venue event that occurs in a chart.
    /// </summary>
    public abstract class VenueEvent : ChartEvent
    {
        private readonly VenueEventFlags _flags;

        public bool IsOptional => (_flags & VenueEventFlags.Optional) != 0;

        public VenueEvent(VenueEventFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            _flags = flags;
        }

        public VenueEvent(double time, double timeLength, uint tick, uint tickLength)
            : this(VenueEventFlags.None, time, timeLength, tick, tickLength)
        {
        }
    }

    /// <summary>
    /// Flags for venue events.
    /// </summary>
    public enum VenueEventFlags
    {
        None = 0,

        Optional = 1 << 0,
    }
}