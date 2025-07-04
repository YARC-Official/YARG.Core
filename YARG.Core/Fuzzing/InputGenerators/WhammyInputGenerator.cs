using System;
using System.Collections.Generic;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Input;

namespace YARG.Core.Fuzzing.InputGenerators
{
    /// <summary>
    /// Generates whammy input sequences with various patterns for testing.
    /// </summary>
    public class WhammyInputGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;

        /// <summary>
        /// Initializes a new instance of WhammyInputGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public WhammyInputGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates whammy input sequence with the specified pattern.
        /// </summary>
        /// <param name="startTime">Start time in seconds</param>
        /// <param name="endTime">End time in seconds</param>
        /// <param name="pattern">Whammy pattern to generate</param>
        /// <returns>Array of whammy inputs</returns>
        public GameInput[] GenerateWhammyInputs(double startTime, double endTime, WhammyPattern pattern)
        {
            if (startTime >= endTime)
                return Array.Empty<GameInput>(); // Return empty array for invalid/zero duration

            return pattern switch
            {
                WhammyPattern.Continuous => GenerateContinuousWhammy(startTime, endTime),
                WhammyPattern.Intermittent => GenerateIntermittentWhammy(startTime, endTime),
                WhammyPattern.RapidToggle => GenerateRapidToggleWhammy(startTime, endTime),
                WhammyPattern.SubFrameTiming => GenerateSubFrameTimingWhammy(startTime, endTime),
                WhammyPattern.EdgeCaseTiming => GenerateEdgeCaseTimingWhammy(startTime, endTime),
                _ => throw new ArgumentException($"Unknown whammy pattern: {pattern}")
            };
        }

        /// <summary>
        /// Generates continuous whammy input at steady intervals.
        /// </summary>
        private GameInput[] GenerateContinuousWhammy(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.1; // 10 times per second

            for (double time = startTime; time < endTime; time += interval)
            {
                inputs.Add(GameInput.Create(time, GuitarAction.Whammy, 1.0f));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates intermittent whammy input with on/off patterns.
        /// </summary>
        private GameInput[] GenerateIntermittentWhammy(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.2; // Change every 0.2 seconds
            bool whammyOn = false;

            for (double time = startTime; time < endTime; time += interval)
            {
                whammyOn = !whammyOn;
                float whammyValue = whammyOn ? 1.0f : 0.0f;
                inputs.Add(GameInput.Create(time, GuitarAction.Whammy, whammyValue));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates rapid toggle whammy input for stress testing.
        /// </summary>
        private GameInput[] GenerateRapidToggleWhammy(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double interval = 0.05; // Very rapid - 20 times per second
            bool whammyOn = false;

            for (double time = startTime; time < endTime; time += interval)
            {
                whammyOn = !whammyOn;
                float whammyValue = whammyOn ? 1.0f : 0.0f;
                inputs.Add(GameInput.Create(time, GuitarAction.Whammy, whammyValue));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates whammy input with sub-frame timing precision.
        /// </summary>
        private GameInput[] GenerateSubFrameTimingWhammy(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double baseInterval = 1.0 / 60.0; // 60 FPS base
            const double maxOffset = 0.001; // ±1ms variation

            for (double time = startTime; time < endTime; time += baseInterval)
            {
                // Add small random offset for sub-frame precision testing
                double offset = (_random.NextDouble() - 0.5) * 2.0 * maxOffset;
                double actualTime = Math.Max(startTime, time + offset); // Ensure time doesn't go below startTime
                
                // Random whammy value for variability
                float whammyValue = (float)_random.NextDouble();
                inputs.Add(GameInput.Create(actualTime, GuitarAction.Whammy, whammyValue));
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates whammy input at critical timing boundaries.
        /// </summary>
        private GameInput[] GenerateEdgeCaseTimingWhammy(double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double epsilon = 1e-6; // Very small time offset

            // Critical timing points
            var criticalTimes = new[]
            {
                startTime,
                startTime + epsilon,
                startTime + 0.001, // 1ms after start
                (startTime + endTime) / 2, // Middle point
                endTime - 0.001, // 1ms before end
                endTime - epsilon,
                endTime
            };

            foreach (var time in criticalTimes)
            {
                if (time >= startTime && time <= endTime)
                {
                    // Alternate between full whammy and no whammy
                    float whammyValue = (Array.IndexOf(criticalTimes, time) % 2 == 0) ? 1.0f : 0.0f;
                    inputs.Add(GameInput.Create(time, GuitarAction.Whammy, whammyValue));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates whammy input with gradual value changes for smooth testing.
        /// </summary>
        public GameInput[] GenerateGradualWhammy(double startTime, double endTime, double interval = 0.1)
        {
            var inputs = new List<GameInput>();
            int stepCount = 0;
            const int totalSteps = 20; // Number of steps for full whammy range

            for (double time = startTime; time < endTime; time += interval)
            {
                // Create a sine wave pattern for smooth whammy changes
                float whammyValue = (float)(0.5 + 0.5 * Math.Sin(2 * Math.PI * stepCount / totalSteps));
                inputs.Add(GameInput.Create(time, GuitarAction.Whammy, whammyValue));
                stepCount++;
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates whammy input with random values and timing for chaos testing.
        /// </summary>
        public GameInput[] GenerateRandomWhammy(double startTime, double endTime, double minInterval = 0.05, double maxInterval = 0.5)
        {
            var inputs = new List<GameInput>();
            double currentTime = startTime;

            while (currentTime < endTime)
            {
                // Random whammy value
                float whammyValue = (float)_random.NextDouble();
                inputs.Add(GameInput.Create(currentTime, GuitarAction.Whammy, whammyValue));

                // Random interval for next input
                double interval = minInterval + (_random.NextDouble() * (maxInterval - minInterval));
                currentTime += interval;
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}