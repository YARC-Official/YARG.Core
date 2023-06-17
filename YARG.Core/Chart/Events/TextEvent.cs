namespace YARG.Core.Chart
{
    /// <summary>
    /// A text event that occurs in a chart.
    /// </summary>
    public class TextEvent : ChartEvent
    {
        public string Text { get; }

        public TextEvent(string text, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Text = text;
        }
    }
}