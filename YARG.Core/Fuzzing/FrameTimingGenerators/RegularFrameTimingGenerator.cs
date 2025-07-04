using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing.FrameTimingGenerators
{
    /// <summary>
    /// Generates consistent frame times at a target frame rate.
    /// </summary>
    public class RegularFrameTimingGenerator
    {
        private readonly double _targetFrameTime;

        /// <summary>
        /// Initializes a new instance of RegularFrameTimingGenerator.
        /// </summary>
        /// <param name="targetFps">Target frames per second (default: 60 FPS)</param>
        public RegularFrameTimingGenerator(double targetFps = 60.0)
        {
            if (targetFps <= 0)
                throw new ArgumentException("Target FPS must be greater than zero", nameof(targetFps));
                
            _targetFrameTime = 1.0 / targetFps;
        }

        /// <summary>
        /// Generates consistent frame times for the specified time range.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <returns>Array of frame times in seconds</returns>
        public double[] GenerateFrameTimes(double startTime, double endTime)
        {
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be less than end time");

            var frameTimes = new List<double>();
            
            double currentTime = startTime;
            while (currentTime < endTime)
            {
                frameTimes.Add(currentTime);
                currentTime += _targetFrameTime;
            }

            return frameTimes.ToArray();
        }

        /// <summary>
        /// Gets the target frame time in seconds.
        /// </summary>
        public double TargetFrameTime => _targetFrameTime;

        /// <summary>
        /// Gets the target frames per second.
        /// </summary>
        public double TargetFps => 1.0 / _targetFrameTime;
    }
}