using System.Collections.Generic;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    public class WaitCountdown : ChartEvent
    {
        public const float MIN_SECONDS = 9;
        public const float END_COUNTDOWN_SECOND = 1f;

        //The time where the countdown should start fading out and overstrums will break combo again
        public double DeactivateTime => TimeEnd - END_COUNTDOWN_SECOND;

        public WaitCountdown(double time, double timeLength, uint tick, uint tickLength) : base(time, timeLength, tick, tickLength)
        {
        }
    }
}