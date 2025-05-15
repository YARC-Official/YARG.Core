using System;
using Cysharp.Text;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.Engine
{
    public struct EngineTimer
    {
        private const double NOT_STARTED = double.MaxValue;

        private double _startTime;
        private double _speed;

        public readonly double TimeThreshold;
        public readonly string Name;

        public double SpeedAdjustedThreshold => TimeThreshold * _speed;

        public readonly double StartTime => _startTime;
        public readonly double EndTime => _startTime + TimeThreshold * _speed;

        public bool IsActive { get; private set; }

        static EngineTimer()
        {
            Utf16ValueStringBuilder.RegisterTryFormat<EngineTimer>(TryFormat);
        }

        public EngineTimer(string name, double threshold)
        {
            _startTime = NOT_STARTED;
            _speed = 1.0;

            Name = name;
            TimeThreshold = threshold;

            IsActive = false;
        }

        public void Start(double currentTime)
        {
            Start(ref _startTime, currentTime);
            IsActive = true;

            YargLogger.LogFormatTrace("Started {0} timer at {1}", Name, currentTime);
        }

        public void StartWithOffset(double currentTime, double offset)
        {
            StartWithOffset(ref _startTime, currentTime, TimeThreshold * _speed, offset);
            IsActive = true;

            YargLogger.LogFormatTrace("Started {0} timer at {1} with offset {2}", Name, currentTime, offset);
        }

        public void Disable(double currentTime, bool early = false)
        {
            // Sanity checks for queued updates
            if (IsActive && YargLogger.IsLevelEnabled(LogLevel.Trace))
            {
                if (early)
                {
                    YargLogger.AssertFormat(
                        currentTime <= EndTime,
                        "{0} timer ended late! Should have ended before or at {1}, but ended at {2}.",
                        Name, EndTime, currentTime
                    );
                }
                else
                {
                    YargLogger.AssertFormat(
                        currentTime == EndTime,
                        "{0} timer end was not synchronized! Should have ended at {1}, but ended at {2} instead.",
                        Name, EndTime, currentTime
                    );
                }
            }

            IsActive = false;
            YargLogger.LogFormatTrace("Disabled {0} timer at {1}", Name, currentTime);
        }

        public void Reset()
        {
            _startTime = NOT_STARTED;
            IsActive = false;
        }

        public readonly bool IsExpired(double currentTime)
        {
            return currentTime >= EndTime;
        }

        public void SetSpeed(double speed)
        {
            _speed = speed;
        }

        public static void Start(ref double startTime, double currentTime)
        {
            startTime = currentTime;
        }

        public static void StartWithOffset(ref double startTime, double currentTime, double threshold, double offset)
        {
            double diff = Math.Abs(threshold - offset);
            startTime = currentTime - diff;
        }

        public static void Reset(ref double startTime)
        {
            startTime = NOT_STARTED;
        }

        public readonly override string ToString()
        {
            if (StartTime == NOT_STARTED)
                return "Not started";

            return $"{StartTime:0.000000} - {EndTime:0.000000}";
        }

        public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            return TryFormat(this, destination, out charsWritten, format);
        }

        private static bool TryFormat(EngineTimer timer, Span<char> dest, out int written, ReadOnlySpan<char> format)
        {
            written = 0;

            if (timer.StartTime == NOT_STARTED)
                return dest.TryWriteAndAdvance("Not started", ref written);

            if (!dest.TryWriteAndAdvance(timer.StartTime, ref written, "0.000000"))
                return false;

            if (!dest.TryWriteAndAdvance(" - ", ref written))
                return false;

            if (!dest.TryWriteAndAdvance(timer.EndTime, ref written, "0.000000"))
                return false;

            return true;
        }
    }
}
