namespace YARG.Core.Chart
{
    /// <summary>
    /// A text event that occurs in a chart.
    /// </summary>
    public class TextEvent : ChartEvent
    {
        public string Text { get; }

        public TextEvent(string text, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            Text = text;
        }
    }
}