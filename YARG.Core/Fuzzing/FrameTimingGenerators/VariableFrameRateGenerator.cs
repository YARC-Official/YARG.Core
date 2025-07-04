using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing.FrameTimingGenerators
{
    /// <summary>
    /// Generates frame times simulating variable frame rates over time.
    /// </summary>
    public class VariableFrameRateGenerator
    {
        private readonly Random _random;
        private readonly double _minFps;
        private readonly double _maxFps;
        private readonly double _fpsChangeInterval;

        /// <summary>
        /// Initializes a new instance of VariableFrameRateGenerator.
        /// </summary>
        /// <param name="minFps">Minimum frames per second (default: 30 FPS)</param>
        /// <param name="maxFps">Maximum frames per second (default: 120 FPS)</param>
        /// <param name="fpsChangeInterval">Interval between FPS changes in seconds (default: 1.0)</param>
        /// <param name="seed">Random seed for reproducible generation</param>
        public VariableFrameRateGenerator(double minFps = 30.0, double maxFps = 120.0, 
            double fpsChangeInterval = 1.0, int? seed = null)
        {
            if (minFps <= 0)
                throw new ArgumentException("Minimum FPS must be greater than zero", nameof(minFps));
            if (maxFps <= minFps)
                throw new ArgumentException("Maximum FPS must be greater than minimum FPS", nameof(maxFps));
            if (fpsChangeInterval <= 0)
                throw new ArgumentException("FPS change interval must be greater than zero", nameof(fpsChangeInterval));

            _minFps = minFps;
            _maxFps = maxFps;
            _fpsChangeInterval = fpsChangeInterval;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates frame times with variable frame rates for the specified time range.
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
            double currentFps = (_minFps + _maxFps) / 2.0; // Start at average FPS
            double nextFpsChange = startTime + _fpsChangeInterval;
            
            while (currentTime < endTime)
            {
                frameTimes.Add(currentTime);
                
                // Check if it's time to change FPS
                if (currentTime >= nextFpsChange)
                {
                    // Random FPS between min and max
                    currentFps = _minFps + (_maxFps - _minFps) * _random.NextDouble();
                    nextFpsChange = currentTime + _fpsChangeInterval;
                }
                
                double frameTime = 1.0 / currentFps;
                currentTime += frameTime;
            }

            return frameTimes.ToArray();
        }

        /// <summary>
        /// Gets the minimum frames per second.
        /// </summary>
        public double MinFps => _minFps;

        /// <summary>
        /// Gets the maximum frames per second.
        /// </summary>
        public double MaxFps => _maxFps;

        /// <summary>
        /// Gets the interval between FPS changes in seconds.
        /// </summary>
        public double FpsChangeInterval => _fpsChangeInterval;

        /// <summary>
        /// Gets the minimum frame time in seconds (based on max FPS).
        /// </summary>
        public double MinFrameTime => 1.0 / _maxFps;

        /// <summary>
        /// Gets the maximum frame time in seconds (based on min FPS).
        /// </summary>
        public double MaxFrameTime => 1.0 / _minFps;
    }
}