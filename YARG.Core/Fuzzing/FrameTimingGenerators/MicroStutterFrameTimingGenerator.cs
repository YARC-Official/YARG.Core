using System;
using System.Collections.Generic;

namespace YARG.Core.Fuzzing.FrameTimingGenerators
{
    /// <summary>
    /// Generates frame times with occasional micro-stutters (brief frame time spikes).
    /// </summary>
    public class MicroStutterFrameTimingGenerator
    {
        private readonly Random _random;
        private readonly double _baseFrameTime;
        private readonly double _stutterProbability;
        private readonly double _stutterMultiplier;

        /// <summary>
        /// Initializes a new instance of MicroStutterFrameTimingGenerator.
        /// </summary>
        /// <param name="baseFps">Base frames per second (default: 60 FPS)</param>
        /// <param name="stutterProbability">Probability of stutter per frame (default: 5%)</param>
        /// <param name="stutterMultiplier">Multiplier for stutter frame time (default: 3x)</param>
        /// <param name="seed">Random seed for reproducible generation</param>
        public MicroStutterFrameTimingGenerator(double baseFps = 60.0, double stutterProbability = 0.05, 
            double stutterMultiplier = 3.0, int? seed = null)
        {
            if (baseFps <= 0)
                throw new ArgumentException("Base FPS must be greater than zero", nameof(baseFps));
            if (stutterProbability < 0 || stutterProbability > 1)
                throw new ArgumentException("Stutter probability must be between 0 and 1", nameof(stutterProbability));
            if (stutterMultiplier <= 1)
                throw new ArgumentException("Stutter multiplier must be greater than 1", nameof(stutterMultiplier));

            _baseFrameTime = 1.0 / baseFps;
            _stutterProbability = stutterProbability;
            _stutterMultiplier = stutterMultiplier;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates frame times with micro-stutters for the specified time range.
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
                
                double frameTime = _baseFrameTime;
                if (_random.NextDouble() < _stutterProbability)
                {
                    frameTime *= _stutterMultiplier;
                }
                
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
        /// Gets the probability of stutter per frame.
        /// </summary>
        public double StutterProbability => _stutterProbability;

        /// <summary>
        /// Gets the stutter frame time multiplier.
        /// </summary>
        public double StutterMultiplier => _stutterMultiplier;

        /// <summary>
        /// Gets the maximum possible frame time during stutters.
        /// </summary>
        public double MaxStutterFrameTime => _baseFrameTime * _stutterMultiplier;
    }
}