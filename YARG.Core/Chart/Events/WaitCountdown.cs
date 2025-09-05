using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    public class WaitCountdown : ChartEvent
    {
        public const double MIN_SECONDS = 9;

        public WaitCountdown(double time, double timeLength, uint tick, uint tickLength) : base(time, timeLength, tick, tickLength)
        {
        }
    }
}