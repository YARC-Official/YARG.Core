namespace YARG.Core.Chart
{
    public class Section : ChartEvent
    {
        public string Name { get; }

        public Section(string name, double time, uint tick) : base(time, 0, tick, 0)
        {
            Name = name;
        }
    }
}