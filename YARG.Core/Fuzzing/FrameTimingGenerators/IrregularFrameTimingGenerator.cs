using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing.FrameTimingGenerators
{
    /// <summary>
    /// Generates frame times with random variations within specified bounds.
    /// </summary>
    public class IrregularFrameTimingGenerator
    {
        private readonly Random _random;
        private readonly double _minFrameTime;
        private readonly double _maxFrameTime;

        /// <summary>
        /// Initializes a new instance of IrregularFrameTimingGenerator.
        /// </summary>
        /// <param name="minFrameTime">Minimum frame time in seconds</param>
        /// <param name="maxFrameTime">Maximum frame time in seconds</param>
        /// <param name="seed">Random seed for reproducible generation</param>
        public IrregularFrameTimingGenerator(double minFrameTime = 0.008, double maxFrameTime = 0.033, int? seed = null)
        {
            if (minFrameTime <= 0)
                throw new ArgumentException("Minimum frame time must be greater than zero", nameof(minFrameTime));
            if (maxFrameTime <= minFrameTime)
                throw new ArgumentException("Maximum frame time must be greater than minimum frame time", nameof(maxFrameTime));

            _minFrameTime = minFrameTime;
            _maxFrameTime = maxFrameTime;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates frame times with random variations for the specified time range.
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
                
                // Random frame time between min and max
                double frameTime = _minFrameTime + (_maxFrameTime - _minFrameTime) * _random.NextDouble();
                currentTime += frameTime;
            }

            return frameTimes.ToArray();
        }

        /// <summary>
        /// Gets the minimum frame time in seconds.
        /// </summary>
        public double MinFrameTime => _minFrameTime;

        /// <summary>
        /// Gets the maximum frame time in seconds.
        /// </summary>
        public double MaxFrameTime => _maxFrameTime;

        /// <summary>
        /// Gets the minimum frames per second (based on max frame time).
        /// </summary>
        public double MinFps => 1.0 / _maxFrameTime;

        /// <summary>
        /// Gets the maximum frames per second (based on min frame time).
        /// </summary>
        public double MaxFps => 1.0 / _minFrameTime;
    }
}