using System;
using System.Collections.Generic;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Fuzzing.FrameTimingGenerators;

namespace YARG.Core.Fuzzing
{
    /// <summary>
    /// Default implementation of IFrameTimingGenerator that provides basic frame timing patterns.
    /// </summary>
    public class DefaultFrameTimingGenerator : IFrameTimingGenerator
    {
        private readonly Random _random;
        private readonly double _minFrameTime;
        private readonly double _maxFrameTime;
        private readonly int? _seed;

        // Individual generators for each pattern
        private readonly RegularFrameTimingGenerator _regularGenerator;
        private readonly IrregularFrameTimingGenerator _irregularGenerator;
        private readonly MicroStutterFrameTimingGenerator _microStutterGenerator;
        private readonly VariableFrameRateGenerator _variableFrameRateGenerator;
        private readonly SubFramePrecisionGenerator _subFramePrecisionGenerator;

        /// <summary>
        /// Initializes a new instance of DefaultFrameTimingGenerator.
        /// </summary>
        /// <param name="minFrameTime">Minimum frame time in seconds</param>
        /// <param name="maxFrameTime">Maximum frame time in seconds</param>
        /// <param name="seed">Random seed for reproducible generation</param>
        public DefaultFrameTimingGenerator(double minFrameTime = 0.008, double maxFrameTime = 0.033, int? seed = null)
        {
            _minFrameTime = minFrameTime;
            _maxFrameTime = maxFrameTime;
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

            // Initialize individual generators
            _regularGenerator = new RegularFrameTimingGenerator(60.0); // 60 FPS default
            _irregularGenerator = new IrregularFrameTimingGenerator(minFrameTime, maxFrameTime, seed);
            _microStutterGenerator = new MicroStutterFrameTimingGenerator(60.0, 0.05, 3.0, seed);
            _variableFrameRateGenerator = new VariableFrameRateGenerator(1.0 / maxFrameTime, 1.0 / minFrameTime, 1.0, seed);
            _subFramePrecisionGenerator = new SubFramePrecisionGenerator(60.0, 1e-6, seed);
        }

        /// <summary>
        /// Generates frame times for the specified time range using the given pattern.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="pattern">Frame timing pattern to use</param>
        /// <returns>Array of frame times in seconds</returns>
        public double[] GenerateFrameTimes(double startTime, double endTime, FrameTimingPattern pattern)
        {
            if (startTime >= endTime)
                return Array.Empty<double>(); // Return empty array for invalid/zero duration

            return pattern switch
            {
                FrameTimingPattern.Regular => GenerateRegularFrameTimes(startTime, endTime),
                FrameTimingPattern.Irregular => GenerateIrregularFrameTimes(startTime, endTime),
                FrameTimingPattern.MicroStutters => GenerateMicroStutterFrameTimes(startTime, endTime),
                FrameTimingPattern.VariableFrameRate => GenerateVariableFrameRateFrameTimes(startTime, endTime),
                FrameTimingPattern.SubFramePrecision => GenerateSubFramePrecisionFrameTimes(startTime, endTime),
                _ => throw new ArgumentException($"Unknown frame timing pattern: {pattern}")
            };
        }

        /// <summary>
        /// Gets all available frame timing patterns.
        /// </summary>
        /// <returns>Array of available patterns</returns>
        public FrameTimingPattern[] GetAvailablePatterns()
        {
            return new[]
            {
                FrameTimingPattern.Regular,
                FrameTimingPattern.Irregular,
                FrameTimingPattern.MicroStutters,
                FrameTimingPattern.VariableFrameRate,
                FrameTimingPattern.SubFramePrecision
            };
        }

        /// <summary>
        /// Generates consistent frame times at 60 FPS.
        /// </summary>
        private double[] GenerateRegularFrameTimes(double startTime, double endTime)
        {
            return _regularGenerator.GenerateFrameTimes(startTime, endTime);
        }

        /// <summary>
        /// Generates frame times with random variations within bounds.
        /// </summary>
        private double[] GenerateIrregularFrameTimes(double startTime, double endTime)
        {
            return _irregularGenerator.GenerateFrameTimes(startTime, endTime);
        }

        /// <summary>
        /// Generates frame times with occasional micro-stutters (brief spikes).
        /// </summary>
        private double[] GenerateMicroStutterFrameTimes(double startTime, double endTime)
        {
            return _microStutterGenerator.GenerateFrameTimes(startTime, endTime);
        }

        /// <summary>
        /// Generates frame times simulating variable frame rates.
        /// </summary>
        private double[] GenerateVariableFrameRateFrameTimes(double startTime, double endTime)
        {
            return _variableFrameRateGenerator.GenerateFrameTimes(startTime, endTime);
        }

        /// <summary>
        /// Generates frame times with high-precision variations to test floating-point precision.
        /// </summary>
        private double[] GenerateSubFramePrecisionFrameTimes(double startTime, double endTime)
        {
            return _subFramePrecisionGenerator.GenerateFrameTimes(startTime, endTime);
        }
    }
}