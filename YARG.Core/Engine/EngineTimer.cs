using System;

namespace YARG.Core.Engine
{
    public struct EngineTimer
    {
        private double _startTime;
        private double _speed;

        public readonly double TimeThreshold;

        public readonly double StartTime => _startTime;
        public readonly double EndTime => _startTime + TimeThreshold * _speed;

        public bool IsActive { get; private set; }

        public EngineTimer(double threshold)
        {
            _startTime = double.MaxValue;
            _speed = 1.0;

            TimeThreshold = threshold;

            IsActive = false;
        }

        public void Start(double currentTime)
        {
            Start(ref _startTime, currentTime);
            IsActive = true;
        }

        public void StartWithOffset(double currentTime, double offset)
        {
            StartWithOffset(ref _startTime, currentTime, TimeThreshold * _speed, offset);
            IsActive = true;
        }

        public void Disable()
        {
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
            startTime = double.MaxValue;
        }
    }
}
