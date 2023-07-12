namespace YARG.Core.Chart
{
    public partial class Beatline : ChartEvent
    {
        public BeatlineType Type { get; }

        public Beatline(BeatlineType type, double time, uint tick) : base(time, 0, tick, 0)
        {
            Type = type;
        }
    }

    public enum BeatlineType
    {
        Measure,
        Strong,
        Weak,
    }
}