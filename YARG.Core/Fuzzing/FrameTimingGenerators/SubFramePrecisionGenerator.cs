using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing.FrameTimingGenerators
{
    /// <summary>
    /// Generates frame times with high-precision variations to test floating-point precision issues.
    /// </summary>
    public class SubFramePrecisionGenerator
    {
        private readonly Random _random;
        private readonly double _baseFrameTime;
        private readonly double _precisionVariation;

        /// <summary>
        /// Initializes a new instance of SubFramePrecisionGenerator.
        /// </summary>
        /// <param name="baseFps">Base frames per second (default: 60 FPS)</param>
        /// <param name="precisionVariation">Precision variation in seconds (default: 1 microsecond)</param>
        /// <param name="seed">Random seed for reproducible generation</param>
        public SubFramePrecisionGenerator(double baseFps = 60.0, double precisionVariation = 1e-6, int? seed = null)
        {
            if (baseFps <= 0)
                throw new ArgumentException("Base FPS must be greater than zero", nameof(baseFps));
            if (precisionVariation <= 0)
                throw new ArgumentException("Precision variation must be greater than zero", nameof(precisionVariation));

            _baseFrameTime = 1.0 / baseFps;
            _precisionVariation = precisionVariation;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates frame times with sub-frame precision variations for the specified time range.
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
                
                // Add small random variation to test floating-point precision
                double variation = (2.0 * _random.NextDouble() - 1.0) * _precisionVariation;
                double frameTime = _baseFrameTime + variation;
                
                // Ensure frame time stays positive and reasonable
                frameTime = Math.Max(frameTime, _baseFrameTime * 0.5);
                
                currentTime += frameTime;
            }

            return frameTimes.ToArray();
        }

        /// <summary>
        /// Gets the base frame time in seconds.
        /// </summary>
        public double BaseFrameTime => _baseFrameTime;

        /// <summary>
        /// Gets the base frames per second.
        /// </summary>
        public double BaseFps => 1.0 / _baseFrameTime;

        /// <summary>
        /// Gets the precision variation in seconds.
        /// </summary>
        public double PrecisionVariation => _precisionVariation;

        /// <summary>
        /// Gets the minimum possible frame time.
        /// </summary>
        public double MinFrameTime => _baseFrameTime - _precisionVariation;

        /// <summary>
        /// Gets the maximum possible frame time.
        /// </summary>
        public double MaxFrameTime => _baseFrameTime + _precisionVariation;
    }
}