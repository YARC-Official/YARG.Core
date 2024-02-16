using System;
using YARG.Core.Chart;

namespace YARG.Core.Engine
{
    public struct TimeContext
    {
        public double Time     { get; private set; }
        public double LastTime { get; private set; }

        public uint Tick     { get; private set; }
        public uint LastTick { get; private set; }

        public static TimeContext Create()
        {
            return new TimeContext
            {
                Time = double.MinValue,
                LastTime = double.MinValue,
                Tick = 0,
                LastTick = 0
            };
        }

        public void UpdateTime(SyncTrack syncTrack, double time)
        {
            if (time < Time)
            {
                YargTrace.Fail($"Time cannot go backwards! Current time: {Time}, new time: {time}");
            }

            // Only update the last time if the current time has changed
            if (Math.Abs(time - Time) > double.Epsilon)
            {
                LastTime = Time;
                LastTick = Tick;
            }

            Time = time;
            Tick = syncTrack.TimeToTick(time);
        }
    }
}