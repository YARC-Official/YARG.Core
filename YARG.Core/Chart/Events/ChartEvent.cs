namespace YARG.Core.Chart
{
    /// <summary>
    /// A general event that occurs in a chart: notes, phrases, text events, etc.
    /// </summary>
    public abstract class ChartEvent
    {
        public double Time { get; }
        public double TimeLength { get; }
        public double TimeEnd => Time + TimeLength;

        public uint Tick { get; }
        public uint TickLength { get; }
        public uint TickEnd => Tick + TickLength;

        public ChartEvent(double time, double timeLength, uint tick, uint tickLength)
        {
            Time = time;
            TimeLength = timeLength;
            Tick = tick;
            TickLength = tickLength;
        }
    }
}